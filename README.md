# Unity2DRoguelikeMultiplayer

Adding network functionality to Unity's 2D Roguelike template project using TcpClient and TcpListener
This is my first real use of Unity and .NET's net sockets.

Basic requirements:
Ability for 2 clients to launch and have the other client represented as another player GameObject.
Allow connection/data transmission on a separate thread to prevent locking the game during synchronous actions.
Add game rules to account for having more than one player
Modify game AI to fairly pursue each player\n

Current State:\n
 _ Game rules/flow\n
  - I don't know the intended game flow but I opted for adding a new player turn for each connected client. Clients can't collide and therefore there is a different exit tile for each player\n
 _ Added Main Menu\n
  - Solo: Launch one player local game as in the tutorial\n
  - Host: Create server TcpListener and move to new Lobby scene\n
  - Join: Create TcpClient and attempt to connect to i.p specified in ip test field and move to lobby scene\n
  - Player name: player name to use when connecting\n
 _ Added Lobby Scene\n
  - Shows connected client and for the host a "start game" button which takes all client to the game scene\n
 _ Added Network Classes\n
  _ Network Manager\n
    - Create TcpListeners for hosts and TcpClients for clients, creates connection and data threads and manages a list of connected peers\n
	- Connection thread listens for new connections and creates a new peer for each incoming connection\n
	- Data thread takes packets from a queue and sends them to the specified clients\n
  _ Network Peer\n
	- Manages state of a connected client\n
	- Stores pending packet information e.g. requested movements and client info (connected state, name etc.)\n
  _ Network Packet\n
    - Stores information to be sent across a network\n
	- Contains functions for writing multiple integer and string values and parsing functions to extract this information after recieving them\n
	- Has multiple packet types allowing specifying of multiple formats of data (e.g. a PLAYER_MOVE packet has an playerId, x directions and y direction)\n
  _Modified BoardManager\n
    - Objects are now only placed by the host and have their positions broadcast to clients to replicate\n
  _Modified Player\n
    - Player movements for clients are now sent to host and not actioned until acknowledged by the host\n
  _Modified Enemy\n
    - Before moving each frame enemies attempt to target the closest player\n
  _Modified GameManager\n
    - Added logic to have multiple player turns\n
	- Added combined "food" levels for a joint score\n
	- Game flow deferred at multiple points for clients waiting for signal from host to do them\n
\n
Known Issues:\n
 _ UI\n
   - The UI in the pre game states is very hacked in order to allow the basic operation of joining and hosting\n
   - Resizing windows messes up UI as it was made for a certain viewport\n
 _ Error handling\n
   - UI doesn't present all network errors e.g. when joining the socket is already in use (due to other app or previous recent game crash (takes windows 2 mins to release socket))\n
 _ Design problems\n
   - We don't handle some weirdness with design e.g. one player is on an exit tile still has to make a move to progress their turn to the next player\n