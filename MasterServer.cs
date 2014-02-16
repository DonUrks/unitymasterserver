// Copyright 2014 DonUrks
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;

namespace Urks
{
	public class MasterServer : UnityEngine.MonoBehaviour
	{
		public bool dedicatedServer = false;
		public string ipAddress = "";
		public int port = 0;
		public int updateRate = 60; 
		
		private bool hostRegistered = false;
		private bool hostUpdated = false;
		
		private TcpClient socket;
		private Stream socketStream;
		
		private string hostId = "";
		private string token = "";
		private string gameName = "";
		private byte[] data;

		// actions to server
		private const char ACTION_REGISTER_HOST = 'a';
		private const char ACTION_UNREGISTER_HOST =  'b';
		private const char ACTION_UPDATE_HOST = 'c';
		private const char ACTION_REQUEST_HOSTS = 'd';
		
		// actions to client
		private const byte ACTION_HOST_REGISTERED = (byte)'e';
		private const byte ACTION_HOSTS = (byte)'f';
		
		private List<UnityEngine.HostData> hostData = new List<UnityEngine.HostData>();

		private void Update()
		{
			if(null != this.socket && this.socket.Connected)
			{
				if(0 < this.socket.Available)
				{
					StreamReader streamReader = new StreamReader(this.socketStream);
                    Stream byteStreamReader = socketStream;
					
					byte[] actionBuffer = new byte[1];
					byteStreamReader.Read(actionBuffer, 0, 1);
					
					switch(actionBuffer[0])
					{
					case ACTION_HOSTS:
						// host count
						byte[] hostCountBuffer = new byte[1];
						byteStreamReader.Read(hostCountBuffer, 0, 1);
						byte hostCount = hostCountBuffer[0];

						for(int i=0; i<hostCount; i++)
						{
							// address
							byte[] addressLengthBuffer = new byte[1];
							byteStreamReader.Read(addressLengthBuffer, 0, 1);
							byte[] addressBuffer = new byte[addressLengthBuffer[0]];
							byteStreamReader.Read(addressBuffer, 0, addressLengthBuffer[0]);

							// port
							byte[] portLengthBuffer = new byte[1];
							byteStreamReader.Read(portLengthBuffer, 0, 1);
							byte[] portBuffer = new byte[portLengthBuffer[0]];
							byteStreamReader.Read(portBuffer, 0, portLengthBuffer[0]);

							// name
							byte[] nameLengthBuffer = new byte[1];
							byteStreamReader.Read(nameLengthBuffer, 0, 1);
							byte[] nameBuffer = new byte[nameLengthBuffer[0]];
							byteStreamReader.Read(nameBuffer, 0, nameLengthBuffer[0]);

							// passwordRequired
							byte[] passwordRequiredBuffer = new byte[1];
							byteStreamReader.Read(passwordRequiredBuffer, 0, 1);

							// playerCount
							byte[] playerCountBuffer = new byte[1];
							byteStreamReader.Read(playerCountBuffer, 0, 1);

							// playerLimit
							byte[] playerLimitBuffer = new byte[1];
							byteStreamReader.Read(playerLimitBuffer, 0, 1);

							string address = System.Text.Encoding.ASCII.GetString(addressBuffer);
							string port = System.Text.Encoding.ASCII.GetString(portBuffer);
							string name = System.Text.Encoding.UTF8.GetString(nameBuffer);
									
							bool passwordRequired = passwordRequiredBuffer[0] == 0 ? false : true;
							int playerCount = (int) playerCountBuffer[0];
							int playerLimit = (int) playerLimitBuffer[0];

							UnityEngine.HostData hostData = new UnityEngine.HostData();
							hostData.connectedPlayers = (int) playerCountBuffer[0];
							hostData.playerLimit = playerLimit;
							hostData.gameName = name;
							hostData.ip = new string[1];
							hostData.ip[0] = address;
							hostData.port = int.Parse(port);
							hostData.passwordProtected = passwordRequired; 

							this.hostData.Add(hostData);

							UnityEngine.Debug.Log(address + " " + port + " " + name + " " + passwordRequired + " " + playerCount + " " + playerLimit);
						}

						break;
					case ACTION_HOST_REGISTERED:	// registered
                        byte[] tokenBuffer = new byte[16];
						streamReader.Read(tokenBuffer, 0, 16);
						
						char[] hostIdLengthBuffer = new char[1];
						streamReader.Read(hostIdLengthBuffer, 0, 1);
						
						char[] hostIdBuffer = new char[hostIdLengthBuffer[0]];
						streamReader.Read(hostIdBuffer, 0, hostIdLengthBuffer[0]);
						
						this.token = new string(tokenBuffer);
						this.hostId = new string(hostIdBuffer);
						
						this.hostRegistered = true;
						StartCoroutine("UpdateHostData");

						UnityEngine.Debug.Log("Host registered on master server.");
						break;
					default:
						UnityEngine.Debug.Log("Unknown action from MasterServer: " + actionBuffer[0]);
						break;
					}
				}
			}
		}
		
		public void RegisterHost(string gameTypeName, string gameName/*, byte[] data = null*/)
		{
			if(!UnityEngine.Network.isServer)
			{
				throw new UnityEngine.UnityException("It's not possible to register a host until it is running.");
			}
			
			this.gameName = gameName;
			//this.data = data;
			
			this.RequireConnection();
			
			StreamWriter streamWriter = new StreamWriter(this.socketStream);
			streamWriter.Write(ACTION_REGISTER_HOST);
			streamWriter.Write((char)System.Text.UTF8Encoding.UTF8.GetByteCount(gameTypeName));
			streamWriter.Write(System.Text.UTF8Encoding.UTF8.GetBytes(gameTypeName));
			streamWriter.Write(UnityEngine.Network.player.port.ToString().Length);
			streamWriter.Write(UnityEngine.Network.player.port.ToString());
			streamWriter.Flush();
		}
		
		public void ClearHostList()
		{
			this.hostData.Clear();
		}
		
		public UnityEngine.HostData[] PollHostList()
		{
			return this.hostData.ToArray();
		}
		
		public void RequestHostList(string gameTypeName)
		{
			this.RequireConnection();

			StreamWriter streamWriter = new StreamWriter(this.socketStream);
			streamWriter.Write(ACTION_REQUEST_HOSTS);
			streamWriter.Write((char)System.Text.UTF8Encoding.UTF8.GetByteCount(gameTypeName));
			streamWriter.Write(gameTypeName);
			streamWriter.Flush();
		}
		
		public void UnregisterHost()
		{
			if(this.hostRegistered)
			{
				StopCoroutine("UpdateHostData");

				this.RequireConnection();
				
				StreamWriter streamWriter = new StreamWriter(this.socketStream);
				streamWriter.Write(ACTION_UNREGISTER_HOST);
				streamWriter.Write((char)this.hostId.Length);
				streamWriter.Write(this.hostId);
				streamWriter.Write(token);
				streamWriter.Flush();
				
				this.token = "";
				this.hostId = "";
				this.gameName = "";
				this.hostRegistered = false;
			}
		}
		
		private void RequireConnection()
		{
			if(null == this.socket || !this.socket.Connected)
			{
				this.socket = new TcpClient(this.ipAddress, this.port);
			}
			this.socketStream = this.socket.GetStream();
		}
		
		IEnumerator UpdateHostData()
		{
			while(true)
			{
				this.RequireConnection();
				
				char passwordRequired = UnityEngine.Network.incomingPassword != "" ? (char)1 : (char)0;
				char maxConnections = (char)UnityEngine.Network.maxConnections;
				
				StreamWriter streamWriter = new StreamWriter(this.socketStream);
				streamWriter.Write(ACTION_UPDATE_HOST);
				streamWriter.Write((char)this.hostId.Length);
				streamWriter.Write(this.hostId);
				streamWriter.Write(token);
				streamWriter.Write((char)System.Text.UTF8Encoding.UTF8.GetByteCount(gameName));
				streamWriter.Write(this.gameName);
				streamWriter.Write(passwordRequired);
				streamWriter.Write((char)UnityEngine.Network.connections.Length);
				streamWriter.Write(maxConnections);
				
				if(this.data != null)
				{
					streamWriter.Write((char)this.data.Length);
					streamWriter.Write(this.data);
				}
				else
				{
					streamWriter.Write((char)0);
				}
				
				streamWriter.Flush();
				
				yield return new UnityEngine.WaitForSeconds(this.updateRate);			
			}
		}
	}
}
