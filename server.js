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

// <config>
// listen on
var port = 8000;		// port for masterserver
var policyPort = 843;		// port for poilcy server (set 0 to disable)
var host = "127.0.0.1";		// listen on address

// timeouts in milliseconds 
var socketTimeout = 15000;	// closes the socket after timeout
var hostTimeout = 180000;	// remove the host from the database after timespan without any updates
var hostTimeoutCheck = 10000;	// check intervall for timeout hosts

// database file
var hostsFile = '';
// </config>

var net = require('net');
var nosql = require('nosql').load(hostsFile);
nosql.description('Hosts database.');

var tokenChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
var currentHostId = -1;

// find highest hostId
nosql.each(
	function(doc)
	{
		var docId = parseInt(doc.hostId);
		
		if(currentHostId < docId)
		{
			currentHostId = docId;
		}
	}
);

// actions to server
var ACTION_REGISTER_HOST = 1;
var ACTION_UNREGISTER_HOST = 2;
var ACTION_UPDATE_HOST = 3;
var ACTION_REQUEST_HOSTS = 4;

// actions to client
var ACTION_HOST_REGISTERED = 5;
var ACTION_HOSTS = 6;

// masterserver
var server = net.createServer();
server.on('connection', onServerConnection);
server.listen(port, host, onServerListen);

// policyserver
if(policyPort > 0)
{
	net.createServer(
		function(socket)
		{
			// todo: configable policy from filesystem
			socket.write("<?xml version=\"1.0\"?>");
			socket.write("<cross-domain-policy>");
			socket.write("<allow-access-from domain=\"*\" />");
			socket.write("</cross-domain-policy>");
			socket.end();
		}
	).listen(policyPort, host);
}

function onServerListen()
{
	console.log('server started');
	setInterval(hostsCleanup, hostTimeoutCheck);
}

function onServerConnection(socket)
{
	console.log('connected: ' + socket.remoteAddress + ':' + socket.remotePort);
	
	// on socket event error: the original remoteAddress and remotePort are "undefined"
	socket.remoteAddressCopy = socket.remoteAddress;
	socket.remotePortCopy = socket.remotePort;
	
	socket.setTimeout(socketTimeout, onSocketTimeout);
	socket.on('end', onSocketEnd);
	socket.on('error', onSocketError);
	socket.on('data', onSocketData);
}

function onSocketTimeout()
{
	socketClose(this);
}

function onSocketEnd()
{
	socketClose(this);
}

function onSocketError()
{
	socketClose(this, true);
}

function socketClose(socket, error)
{
	var log = 'disconnected: ' + socket.remoteAddressCopy + ':' + socket.remotePortCopy;
	if(error)
	{
		log += " error (client process terminated?)";
	}

	console.log(log);

	socket.end();
	socket.destroy();
}

function onSocketData(data)
{
	console.log('incoming: ' + this.remoteAddress + ':' + this.remotePort);
	console.log('data length: ' + data.length);
	console.log('data: ' + data);
			
	try
	{
		var action = data.readUInt8(0);
		var actionBody = data.slice(1);
	}
	catch(err)
	{
		console.log("malformed data: " + err);
		socketClose(this);
		return;
	}
	
	switch(action)
	{
		case ACTION_REGISTER_HOST:
			registerHost(this, actionBody);
			break;
		case ACTION_UNREGISTER_HOST:
			unregisterHost(this, actionBody);
			break;
		case ACTION_UPDATE_HOST:
			updateHost(this, actionBody);
			break;
		case ACTION_REQUEST_HOSTS:
			requestHosts(this, actionBody);
			break;
		default: 		
			console.log("unknown action: " + action);
			socketClose(this);
			break;
		
	}
}

function registerHost(socket, data)
{
	var gameName = "";
	var port = "";
	var useNat = false;
	var playerGUID = "";

	try
	{
		var offset = 0;
	
		// gameName
		var gameNameLength = data.readUInt8(offset);				
		gameName = data.toString("utf8", offset+1, offset+1+gameNameLength);
		offset += 1 + gameNameLength;
		
		// port
		var portLength = data.readUInt8(offset);
		port = data.toString("ascii", offset+1, offset+1+portLength);
		offset += 1 + portLength;
		
		// useNat
		var useNat = data.readUInt8(offset) > 0 ? true : false;
		offset += 1;
		
		// playerGUID
		var playerGUIDLength = data.readUInt8(offset);		
		playerGUID = data.toString("ascii", offset+1, offset+1+playerGUIDLength);
		offset += 1 + playerGUIDLength;
		
	}
	catch(err)
	{
		console.log("malformed registerHost: " + err);
		socketClose(socket);
		return;
	}

	var token = "";
	for(var i=0; i<16; i++)
	{
        token += tokenChars.charAt(Math.floor(Math.random() * tokenChars.length));
	}
	
	currentHostId++;
	var currentId = currentHostId.toString();
	
	var id = nosql.insert(
		{
			"hostId": currentId,
			"gameName": gameName, 
			"token": token,
			"ready": false,
			"address": socket.remoteAddressCopy,
			"port": port,
			"useNat": useNat,
			"playerGUID": playerGUID,
			"timestamp": new Date().getTime()
		}
	);
	
	console.log("host registered");
	console.log("hostId: " + currentId);
	console.log("token: " + token);
	console.log("gameName: " + gameName);
	console.log("port: " + port);
	console.log("useNat: " + useNat);
	console.log("playerGUID: " + playerGUID);
	
	var currentIdLength = Buffer.byteLength(currentId, "ascii");
	
	var hostRegistered = new Buffer(currentIdLength+18);	
	hostRegistered.writeUInt8(ACTION_HOST_REGISTERED, 0);
	hostRegistered.write(token, "ascii", 1);
	hostRegistered.writeUInt8(currentIdLength, 17);
	hostRegistered.write(currentId, 'ascii', 18);
	
	socket.write(hostRegistered);
}

function unregisterHost(socket, data)
{
	var hostId = "";
	var token = "";
	
	try
	{
		var offset = 0;
					
		var hostIdLength = data.readUInt8(offset);
		offset += 1;
		
		hostId = data.toString('ascii', offset, offset+hostIdLength);
		offset += hostIdLength;
		
		token = data.toString('ascii', offset, offset+16);
		offset += 16;
	}
	catch(err)
	{
		console.log("malformed unregisterHost: " + err);
		socketClose(socket);
		return;
	}
	
	nosql.remove(
		function(doc) 
		{
			return doc.hostId == hostId && doc.token == token;
		},
		function(count) 
		{		
			if(count)
			{
				console.log("host unregistered");
				console.log("hostId: " + hostId);
				console.log("token: " + token);
			}
			else
			{
				console.log("host not found");
				console.log("hostId: " + hostId);
				console.log("token: " + token);
				socketClose(socket);
			}
		}
	);	
}

function updateHost(socket, data)
{
	var hostId = "";
	var token = "";
	var name = "";
	var passwordRequired = 0;
	var playerCount = 0;
	var playerLimit = 0;
	var customData;
	
	try
	{
		var offset = 0;
					
		var hostIdLength = data.readUInt8(offset);
		offset += 1;
		
		hostId = data.toString('ascii', offset, offset+hostIdLength);
		offset += hostIdLength;
		
		token = data.toString('ascii', offset, offset+16);
		offset += 16;
		
		var nameLength = data.readUInt8(offset);
		offset += 1;
		
		name = data.toString('utf8', offset, offset+nameLength);
		offset += nameLength;
		
		passwordRequired = data.readUInt8(offset);
		offset += 1;
		
		playerCount = data.readUInt8(offset);
		offset += 1;
		
		playerLimit = data.readUInt8(offset);
		offset += 1;
		
		var customDataLength = data.readUInt8(offset);
		offset += 1;
		
		customData = data.slice(offset, offset+customDataLength);
	}
	catch(err)
	{
		console.log("malformed updateHost: " + err);
		socketClose(socket);
		return;
	}
		
	nosql.update(
		function(doc) 
		{
			if (doc.hostId == hostId && doc.token == token)
			{
				doc.ready = true;
				doc.name = name;
				doc.passwordRequired = passwordRequired;
				doc.playerCount = playerCount;
				doc.playerLimit = playerLimit;
				doc.customData = customData;
				doc.timestamp = new Date().getTime();
			}			
			return doc;
		},
		function(count) 
		{
			if(count)
			{
				console.log("host updated");
				console.log("hostId: " + hostId);
				console.log("token: " + token);
				console.log("name: " + name);
				console.log("passwordRequired: " + passwordRequired);
				console.log("playerCount: " + playerCount);
				console.log("playerLimit: " + playerLimit);
				console.log("customData: " + customData);
			}
			else
			{
				console.log("host not found");
				console.log("hostId: " + hostId);
				console.log("token: " + token);
				console.log("name: " + name);
				socketClose(socket);
			}
		}
	);
}

function requestHosts(socket, data)
{
	var gameName = "";
	
	try
	{
		var gameNameLength = data.readUInt8(0);
		gameName = data.toString("utf8", 1, 1+gameNameLength);
	}
	catch(err)
	{
		console.log("malformed requestHosts: " + err);
		socketClose(socket);
		return;
	}
	
	console.log("request hosts '" + gameName + "'");
	
	nosql.all(
		function(doc) 
		{
			if (doc.gameName == gameName && doc.ready == true)
			{
				return doc;
			}
		}, 
		function(selected)
		{		
			var outputBuffer = new Buffer(2);
			outputBuffer.writeUInt8(ACTION_HOSTS, 0);
			outputBuffer.writeUInt8(selected.length, 1);
			selected.forEach(
				function(doc) 
				{				
					var offset = 0;
				
					var bufferSize = 1 + doc.address.length;	// addressLength + address
					bufferSize += 1 + doc.port.length;	// portLength + port
					bufferSize += 1 + Buffer.byteLength(doc.name, "utf8");	// nameLength + name
					bufferSize += 1;	// passwordRequired
					bufferSize += 1;	// playerCount
					bufferSize += 1;	// playerLimit
					bufferSize += 1;	// useNat
					bufferSize += 1 + Buffer.byteLength(doc.playerGUID, "ascii");	// playerGUIDLength + playerGUID
					
					var documentBuffer = new Buffer(bufferSize);
					
					// address
					documentBuffer.writeUInt8(doc.address.length, offset);
					offset += 1;
					documentBuffer.write(doc.address, "ascii", offset);
					offset += doc.address.length;
					
					// port
					documentBuffer.writeUInt8(doc.port.length, offset);
					offset += 1;
					documentBuffer.write(doc.port, "ascii", offset);
					offset += doc.port.length;
					
					// name
					documentBuffer.writeUInt8(Buffer.byteLength(doc.name, "utf8"), offset);
					offset += 1;
					documentBuffer.write(doc.name, "utf8", offset);
					offset += Buffer.byteLength(doc.name, "utf8");
					
					// passwordRequired
					documentBuffer.writeUInt8(doc.passwordRequired, offset);
					offset += 1;
					
					// playerCount
					documentBuffer.writeUInt8(doc.playerCount, offset);
					offset += 1;
					
					// playerLimit
					documentBuffer.writeUInt8(doc.playerLimit, offset);
					offset += 1;
					
					// useNat
					documentBuffer.writeUInt8(doc.useNat, offset);
					offset += 1;
					
					// playerGUID
					documentBuffer.writeUInt8(doc.playerGUID.length, offset);
					offset += 1;
					documentBuffer.write(doc.playerGUID, "ascii", offset);
					offset += doc.playerGUID.length;
									
					outputBuffer += documentBuffer;
				}
			);
			
			socket.write(outputBuffer);
		}
	);
}

function hostsCleanup()
{
	var timeoutTimestamp = new Date().getTime() - hostTimeout;
	
	nosql.remove(
		function(doc) 
		{
			if(doc.timestamp <= timeoutTimestamp)
			{
				console.log("host timeout: " + doc.hostId);
				return true;
			}
			return false;
		}
	);	
}
