# Unity2DRoguelikeMultiplayer

Adding network functionality to Unity's 2D Roguelike template project using TcpClient and TcpListener

I was recently tasked with achieveing the above. This is my first real use of Unity and .NET's net sockets.

Basic requirements:
Ability for 2 clients to launch and have the other client represented as another player GameObject.
Allow connection/data transmission on a separate thread to prevent locking the game during synchronous actions.
Add game rules to account for having more than one player
Modify game AI to fairly pursue each player
