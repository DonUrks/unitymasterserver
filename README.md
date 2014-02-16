Unity MasterServer written in NodeJS
=================

This program is an alternative to the Unity MasterServer functionality. It includes a working UnregisterHost function :).

Dependencies:
- NodeJS installation
- NodeJS module "NoSQL" 

Setup:
- copy the server.js file to your server
- link the server.js to the NoSQL module

Configuration:
- open server.js and edit the parameters in <config> section
- don't forget to set the hosts file parameter
- to disable the policy server set the policy port to 0 (please note: for webplayer applications the policy server is required see: https://docs.unity3d.com/Documentation/Manual/SecuritySandbox.html)

Unity:
- use the Urks.MasterServer class
- the interface is similar to the Unity MasterServer class (UnityEngine.MasterServer)

ToDo:
- security check (buffer overflow ...)
- implement NAT functionality
- correct "isDedicated" handling
- correct error handling
- only use the byteStreamReader 
