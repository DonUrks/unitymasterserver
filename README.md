Unity MasterServer written in NodeJS
=================

This program is an alternative to the Unity MasterServer functionality. It includes a working UnregisterHost function :) and a policy server for webplayer applications. For webplayer applications the policy server is required see: https://docs.unity3d.com/Documentation/Manual/SecuritySandbox.html.

### Dependencies
- NodeJS installation
- NodeJS module "NoSQL"

### Server Guide
1. Install NodeJS on your server (http://nodejs.org/)
2. Install the NoSQL module: npm install nosql
3. Copy the server.js to your server
4. Change to the directory where the server.js is located
5. Link the NoSQL modul to the directory:	npm link nosql
6. Open the server.js and edit the config section
7. Start the server: node server.js
8. You should see in the console: "server started"

##### Server Configuration
| config        | description |
| ------------- |-------------|
| port      | listen port for the masterserver |
| policyPort | listen port for the policyServer; set 0 to disable |
| host | listen host for the servers |
| socketTimeout | timeout in milliseconds for inactive sockets |
| hostTimeout | timeout in milliseconds for inactive hosts without any updates |
| hostTimeoutCheck | check timer in milliseconds for outdated hosts |
| hostsFile | directory + filename for the NoSQL database file |

### Unity Guide
1. Create a empty gameobject
2. Attach the Urks.MasterServer-Script to it
3. Configure the script in the inspector (especially host and port)
4. Tag the GameObject (for example: "Urks.MasterServer")
5. Obtain in your script the "Urks.MasterServer" object and gain access to the script component
6. Now you can use all methods similiar to the built-in masterserver

##### Example Usage
```javascript
Urks.MasterServer ms = UnityEngine.GameObject.FindGameObjectWithTag("Urks.MasterServer").GetComponent<Urks.MasterServer>();
ms.RegisterHost("TheNextMinecraftClone", "Free for all!", true);
```

### ToDo
- security check (buffer overflow ...)
- correct error handling
- policy xml customizable on server filesystem
