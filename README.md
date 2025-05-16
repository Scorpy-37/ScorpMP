# ScorpMP
ScorpMP is a barebones TCP-based multiplayer system for Unity that works generally like a Minecraft server where one hosts a server and others join via IP
I advise checking out the example scene if you want to use this, though make sure to set the ScorpMP_Server gameObject to inactive when building the game for testing

# Classes
ScorpMP_Server.cs: Listens for connections, manages data receiving and data sending
ScorpMP_ServerLogic.cs: Used for handling custom server-side logic

ScorpMP_Client.cs: Established a connection with the server
ScorpMP_ClientLogic.cs: Used for handling custom client-side logic

ScorpMP_PlayerList.cs: Contains a list of all connected client-side players and functions for creating/removing player objects
ScorpMP_LocalPlayer.cs: Manages local player position and rotation updates
ScorpMP_GlobalPlayer.cs: Houses information about other players (placeholder)
