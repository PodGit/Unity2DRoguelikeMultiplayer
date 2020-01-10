# Unity2DRoguelikeMultiplayer

Adding network functionality to Unity's 2D Roguelike template project using TcpClient and TcpListener
This is my first real use of Unity and .NET's net sockets.

Basic requirements:
Ability for 2 clients to launch and have the other client represented as another player GameObject.
Allow connection/data transmission on a separate thread to prevent locking the game during synchronous actions.
Add game rules to account for having more than one player
Modify game AI to fairly pursue each player

Current State:
 - Game rules/flow
  - I don't know the intended game flow but I opted for adding a new player turn for each connected client. Clients can't collide and therefore there is a different exit tile for each player
 - Added Main Menu
  - Solo: Launch one player local game as in the tutorial
  - Host: Create server TcpListener and move to new Lobby scene
  - Join: Create TcpClient and attempt to connect to i.p specified in ip test field and move to lobby scene
  - Player name: player name to use when connecting
 - Added Lobby Scene
  - Shows connected client and for the host a "start game" button which takes all client to the game scene
 - Added Network Classes
  - Network Manager
    - Create TcpListeners for hosts and TcpClients for clients, creates connection and data threads and manages a list of connected peers
	- Connection thread listens for new connections and creates a new peer for each incoming connection
	- Data thread takes packets from a queue and sends them to the specified clients
  - Network Peer
	- Manages state of a connected client
	- Stores pending packet information e.g. requested movements and client info (connected state, name etc.)
  - Network Packet
    - Stores information to be sent across a network
	- Contains functions for writing multiple integer and string values and parsing functions to extract this information after recieving them
	- Has multiple packet types allowing specifying of multiple formats of data (e.g. a PLAYER_MOVE packet has an playerId, x directions and y direction)
  - Modified BoardManager
    - Objects are now only placed by the host and have their positions broadcast to clients to replicate
  - Modified Player
    - Player movements for clients are now sent to host and not actioned until acknowledged by the host
  - Modified Enemy
    - Before moving each frame enemies attempt to target the closest player
  - Modified GameManager
    - Added logic to have multiple player turns
	- Added combined "food" levels for a joint score
	- Game flow deferred at multiple points for clients waiting for signal from host to do them

Known Issues:
 - UI
   - The UI in the pre game states is very hacked in order to allow the basic operation of joining and hosting
   - Resizing windows messes up UI as it was made for a certain viewport
 - Error handling
   - UI doesn't present all network errors e.g. when joining the socket is already in use (due to other app or previous recent game crash (takes windows 2 mins to release socket))
 - Design problems
   - We don't handle some weirdness with design e.g. one player is on an exit tile still has to make a move to progress their turn to the next player