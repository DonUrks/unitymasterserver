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
		
		private TcpClient socket;
		private Stream socketStream;
		
		private string hostId = "";
		private string token = "";
		private string gameName = "";
		private byte[] data;
		
		// actions to server
		private const byte ACTION_REGISTER_HOST = 1;
		private const byte ACTION_UNREGISTER_HOST = 2;
		private const byte ACTION_UPDATE_HOST = 3;
		private const byte ACTION_REQUEST_HOSTS = 4;
		
		// actions to client
		private const byte ACTION_HOST_REGISTERED = 5;
		private const byte ACTION_HOSTS = 6;
		
		private List<UnityEngine.HostData> hostData = new List<UnityEngine.HostData>();
		
		private void Update()
		{
			if (null != this.socket && this.socket.Connected)
			{
				if (0 < this.socket.Available)
				{
					Urks.MasterServer ms = UnityEngine.GameObject.FindGameObjectWithTag("Urks.MasterServer").GetComponent<Urks.MasterServer>();
					ms.RegisterHost("TheNextMinecraftClone", "Free for all!", true);
					
					byte action = (byte)this.socketStream.ReadByte();
					
					switch(action)
					{
					case ACTION_HOST_REGISTERED:
						// token
						byte[] tokenBytes = new byte[16];
						this.socketStream.Read(tokenBytes, 0, tokenBytes.Length);

						// hostId
						byte hostIdLength = (byte)this.socketStream.ReadByte();
						byte[] hostIdBytes = new byte[hostIdLength];
						this.socketStream.Read(hostIdBytes, 0, hostIdLength);
						
						this.token = System.Text.Encoding.ASCII.GetString(tokenBytes);
						this.hostId = System.Text.Encoding.ASCII.GetString(hostIdBytes);
						
						this.hostRegistered = true;
						StartCoroutine("UpdateHostData");
						
						UnityEngine.Debug.Log("Host registered on master server. (hostId: " + this.hostId + ")");
						break;

					case ACTION_HOSTS:
						// host count
						byte hostCount = (byte)this.socketStream.ReadByte();

						UnityEngine.Debug.Log("Hosts: " + hostCount);

						for(int i = 0; i < hostCount; i++)
						{
							// address
							byte addressLength = (byte)this.socketStream.ReadByte();
							byte[] adressBytes = new byte[addressLength];
							this.socketStream.Read(adressBytes, 0, addressLength);

							// port
							byte portLength = (byte)this.socketStream.ReadByte();
							byte[] portBytes = new byte[portLength];
							this.socketStream.Read(portBytes, 0, portLength);

							// name
							byte nameLength = (byte)this.socketStream.ReadByte();
							byte[] nameBytes = new byte[nameLength];
							this.socketStream.Read(nameBytes, 0, nameLength);

							// passwordRequired
							byte passwordRequired = (byte)this.socketStream.ReadByte();

							// playerCount
							byte playerCount = (byte)this.socketStream.ReadByte();

							// playerLimit
							byte playerLimit = (byte)this.socketStream.ReadByte();

							// useNat
							byte useNat = (byte)this.socketStream.ReadByte();

							// playerGUID
							byte playerGUIDLength = (byte)this.socketStream.ReadByte();
							byte[] playerGUIDBytes = new byte[playerGUIDLength];
							this.socketStream.Read(playerGUIDBytes, 0, playerGUIDLength);

							UnityEngine.HostData hostData = new UnityEngine.HostData();
							hostData.ip = new string[1];
							hostData.ip[0] = System.Text.ASCIIEncoding.ASCII.GetString(adressBytes);
							hostData.port = int.Parse(System.Text.ASCIIEncoding.ASCII.GetString(portBytes));
							hostData.gameName = System.Text.UTF8Encoding.UTF8.GetString(nameBytes);
							hostData.passwordProtected = passwordRequired > 0 ? true : false;
							hostData.connectedPlayers = playerCount;
							hostData.playerLimit = playerLimit;
							hostData.useNat = useNat > 0 ? true : false;
							hostData.guid = System.Text.ASCIIEncoding.ASCII.GetString(playerGUIDBytes);

							this.hostData.Add(hostData);
						}

						break;
					default:
						UnityEngine.Debug.Log("Unknown action from MasterServer: " + action);
						this.socketStream.Close();
						break;
					}
				}
			}
		}

		public void RegisterHost(string gameTypeName, string gameName, bool useNat, byte[] data)
		{
			this.data = data;
			this.RegisterHost(gameTypeName, gameName, useNat);
		}

		public void RegisterHost(string gameTypeName, string gameName, bool useNat)
		{
			if (!UnityEngine.Network.isServer)
			{
				throw new UnityEngine.UnityException("It's not possible to register a host until it is running.");
			}

			this.gameName = gameName;
			
			byte[] gameTypeNameBytes = System.Text.UTF8Encoding.UTF8.GetBytes(gameTypeName);
			if (gameTypeNameBytes.Length <= 0 || gameTypeNameBytes.Length > 255)
			{
				throw new UnityEngine.UnityException("You must pass a GameTypeName 1-255 bytes.");
			}
			
			byte[] portBytes = System.Text.ASCIIEncoding.ASCII.GetBytes(UnityEngine.Network.player.port.ToString());
			byte[] playerGUIDBytes = System.Text.ASCIIEncoding.ASCII.GetBytes(UnityEngine.Network.player.guid);
			
			List<byte> bytes = new List<byte>();
			
			// action
			bytes.Add(ACTION_REGISTER_HOST);
			
			// gameTypeName
			bytes.Add((byte)gameTypeNameBytes.Length);
			bytes.AddRange(gameTypeNameBytes);
			
			// port
			bytes.Add((byte)portBytes.Length);
			bytes.AddRange(portBytes);
			
			// useNat
			bytes.Add((byte)(useNat == true ? 1 : 0));
			
			// player GUID
			bytes.Add((byte)playerGUIDBytes.Length);
			bytes.AddRange(playerGUIDBytes);
			
			this.RequireConnection();
			this.socketStream.Write(bytes.ToArray(), 0, bytes.Count);
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
			byte[] gameTypeNameBytes = System.Text.UTF8Encoding.UTF8.GetBytes(gameTypeName);

			List<byte> bytes = new List<byte>();

			// action
			bytes.Add(ACTION_REQUEST_HOSTS);

			// gameTypeName
			bytes.Add((byte)gameTypeNameBytes.Length);
			bytes.AddRange(gameTypeNameBytes);

			this.RequireConnection();
			this.socketStream.Write(bytes.ToArray(), 0, bytes.Count);
		}
		
		public void UnregisterHost()
		{
			if (this.hostRegistered)
			{
				StopCoroutine("UpdateHostData");

				byte[] hostIdBytes = System.Text.ASCIIEncoding.ASCII.GetBytes(this.hostId);
				byte[] tokenBytes = System.Text.ASCIIEncoding.ASCII.GetBytes(this.token);

				List<byte> bytes = new List<byte>();
				
				// action
				bytes.Add(ACTION_UNREGISTER_HOST);

				// hostId
				bytes.Add((byte)hostIdBytes.Length);
				bytes.AddRange(hostIdBytes);

				// token
				bytes.AddRange(tokenBytes);
				
				this.RequireConnection();
				this.socketStream.Write(bytes.ToArray(), 0, bytes.Count);
			}
		}
		
		private void RequireConnection()
		{
			if (null == this.socket || !this.socket.Connected)
			{
				this.socket = new TcpClient(this.ipAddress, this.port);
				this.socket.NoDelay = true;
			}
			this.socketStream = this.socket.GetStream();
		}
		
		private IEnumerator UpdateHostData()
		{
			byte[] hostIdBytes = System.Text.ASCIIEncoding.ASCII.GetBytes(this.hostId);
			byte[] tokenBytes = System.Text.ASCIIEncoding.ASCII.GetBytes(this.token);
			byte[] gameNameBytes = System.Text.UTF8Encoding.UTF8.GetBytes(this.gameName);

			while (true)
			{
				byte passwordRequired = UnityEngine.Network.incomingPassword != "" ? (byte)1 : (byte)0;
				byte playerCount = (byte)UnityEngine.Network.connections.Length;
				if(!this.dedicatedServer)
				{
					playerCount += 1;
				}
				byte playerLimit = (byte)UnityEngine.Network.maxConnections;

				List<byte> bytes = new List<byte>();

				// action
				bytes.Add(ACTION_UPDATE_HOST);

				// hostId
				bytes.Add((byte)hostIdBytes.Length);
				bytes.AddRange(hostIdBytes);
				
				// token
				bytes.AddRange(tokenBytes);

				// gameName
				bytes.Add((byte)gameNameBytes.Length);
				bytes.AddRange(gameNameBytes);

				// passwordRequired
				bytes.Add(passwordRequired);

				// playerCount
				bytes.Add(playerCount);

				// playerLimit
				bytes.Add(playerLimit);

				// data
				if (this.data != null)
				{
					bytes.Add((byte)this.data.Length);
					bytes.AddRange(this.data);
				}
				else
				{
					bytes.Add(0);
				}

				this.RequireConnection();
				this.socketStream.Write(bytes.ToArray(), 0, bytes.Count);
				
				yield return new UnityEngine.WaitForSeconds(this.updateRate);
			}
		}
	}
}
