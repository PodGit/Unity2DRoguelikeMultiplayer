﻿    using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

namespace Completed
{
	using System.Collections.Generic;       //Allows us to use Lists. 
    using System.Threading;
    using UnityEngine.UI;					//Allows us to use UI.
	
	public class GameManager : MonoBehaviour
	{
        struct PendingMove
        {
            public int playerId;
            public Vector3 movement;
        }

		public float levelStartDelay = 2f;						//Time to wait before starting level, in seconds.
		public float turnDelay = 0.1f;							//Delay between each Player turn.
		public static GameManager instance = null;				//Static instance of GameManager which allows it to be accessed by any other script.
		[HideInInspector] public int playersTurn = 0;		    //Boolean to check if it's players turn, hidden in inspector but public.
        public GameObject PlayerPrefab;


        private Text levelText;									//Text to display current level number.
		private GameObject levelImage;							//Image to block out level as levels are being set up, background for levelText.
		private BoardManager boardScript;						//Store a reference to our BoardManager which will set up the level.
		private int level = 0;									//Current level number, expressed in game as "Day 1".
		private List<Enemy> enemies;							//List of all Enemy units, used to issue them move commands.
		private bool enemiesMoving;								//Boolean to check if enemies are moving.
		private bool doingSetup = true;                         //Boolean to check if we're setting up board, prevent Player from moving during setup.
        private GameObject[] players;

        //Network
        public const int MaxNumPlayers = 4;
        public int startFoodPoints = 100;
        public int numPlayers { get; set; } = 1;
        private int[] playerFoodPoints;
        private bool clientReadyToStart = false;

        private Mutex pendingMovementMutex;
        private Queue<PendingMove> pendingMovements;

        GameManager()
        {
            pendingMovements = new Queue<PendingMove>();
            pendingMovementMutex = new Mutex();
        }

        //Awake is always called before any Start functions
        void Awake()
		{
            //Check if instance already exists
            if (instance == null)

                //if not, set instance to this
                instance = this;

            //If instance already exists and it's not this:
            else if (instance != this)

                //Then destroy this. This enforces our singleton pattern, meaning there can only ever be one instance of a GameManager.
                Destroy(gameObject);	
			
			//Sets this to not be destroyed when reloading scene
			DontDestroyOnLoad(gameObject);
			
			//Assign enemies to a new List of Enemy objects.
			enemies = new List<Enemy>();
			
			//Get a component reference to the attached BoardManager script
			boardScript = GetComponent<BoardManager>();;
		}

        //this is called only once, and the paramter tell it to be called only after the scene was loaded
        //(otherwise, our Scene Load callback would be called the very first load, and we don't want that)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static public void CallbackInitialization()
        {
            //register the callback to be called everytime the scene is loaded
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        //This is called each time a scene is loaded.
        static private void OnSceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
            bool isNetworkClient = (NetworkManager.Instance.IsActive() &&
                !NetworkManager.Instance.IsHost());

            if (!isNetworkClient && arg0.name == "GameScene")
            {
                instance.level++;
                instance.InitGame();
            }
        }
		
		//Initializes the game for each level.
		void InitGame()
		{
            playersTurn = 0;

            if (NetworkManager.Instance.IsActive())
            {
                numPlayers = NetworkManager.Instance.GetNumPeers();
            }
            else numPlayers = 1;

			//While doingSetup is true the player can't move, prevent player from moving while title card is up.
			doingSetup = true;
			
			//Get a reference to our image LevelImage by finding it by name.
			levelImage = GameObject.Find("LevelImage");
			
			//Get a reference to our text LevelText's text component by finding it by name and calling GetComponent.
			levelText = GameObject.Find("LevelText").GetComponent<Text>();
			
			//Set the text of levelText to the string "Day" and append the current level number.
			levelText.text = "Day " + level;
			
			//Set levelImage to active blocking player's view of the game board during setup.
			levelImage.SetActive(true);
			
			//Call the HideLevelImage function with a delay in seconds of levelStartDelay.
			Invoke("HideLevelImage", levelStartDelay);
			
			//Clear any Enemy objects in our List to prepare for next level.
			enemies.Clear();

            //Call the SetupScene function of the BoardManager script, pass it current level number.
            boardScript.SetupScene(level);

            playerFoodPoints = new int[MaxNumPlayers];
            for (int playerIdx = 0; playerIdx < MaxNumPlayers; ++playerIdx)
            {
                playerFoodPoints[playerIdx] = startFoodPoints;
            }

            SpawnPlayers();
        }

        void SpawnPlayers()
        {
            bool networkActive = NetworkManager.Instance.IsActive();

            players = new GameObject[numPlayers];

            for (int playerIdx = 0; playerIdx < numPlayers; ++playerIdx)
            {
                Debug.Assert(PlayerPrefab != null, "Player prefab not set");

                Vector3 spawnVector = new Vector3(playerIdx, 0, 0);
                players[playerIdx] = Instantiate(PlayerPrefab, spawnVector, Quaternion.identity);
                Player spawnedPlayer = players[playerIdx].GetComponent<Player>();

                spawnedPlayer.playerId = playerIdx;
                spawnedPlayer.Local = true;

                if (networkActive &&
                    NetworkManager.Instance.GetMyPeer().GetPeerId() != playerIdx)
                {
                    spawnedPlayer.Local = false;
                }
            }
        }

        //Hides black image used between levels
        void HideLevelImage()
		{
			//Disable the levelImage gameObject.
			levelImage.SetActive(false);
			
			//Set doingSetup to false allowing player to move again.
			doingSetup = false;
		}
		
		//Update is called every frame.
		void Update()
		{
            if (clientReadyToStart)
            {
                InitGame();
                clientReadyToStart = false;
            }


            pendingMovementMutex.WaitOne();
            while (pendingMovements.Count > 0)
            {
                PendingMove move = pendingMovements.Dequeue();
                Player player = players[move.playerId].GetComponent<Player>();

                if (NetworkManager.Instance.IsActive())
                {
                    NetworkPeer peer = NetworkManager.Instance.GetPeer(move.playerId);
                    peer.SetRequestedMovement(Player.MovementDirection.none);
                }

                Vector3 position = player.transform.position;
                player.RemoteMove((int)move.movement.x, (int)move.movement.y);

                SetNextPlayersTurn();                
            }
            pendingMovementMutex.ReleaseMutex();

			//Check that playersTurn or enemiesMoving or doingSetup are not currently true.
			if(playersTurn > -1 || enemiesMoving || doingSetup)
				
				//If any of these are true, return and do not start MoveEnemies.
				return;
			
			//Start moving enemies.
			StartCoroutine (MoveEnemies ());
		}
		
		//Call this to add the passed in Enemy to the List of Enemy objects.
		public void AddEnemyToList(Enemy script)
		{
			//Add Enemy to List enemies.
			enemies.Add(script);
		}
		
        public int GetNumEnemies()
        {
            return enemies.Count;
        }
		
		//GameOver is called when the player reaches 0 food points
		public void GameOver()
		{
			//Set levelText to display number of levels passed and game over message
			levelText.text = "After " + level + " days, you starved.";
			
			//Enable black background image gameObject.
			levelImage.SetActive(true);
			
			//Disable this GameManager.
			enabled = false;
		}
		
		//Coroutine to move enemies in sequence.
		IEnumerator MoveEnemies()
		{
			//While enemiesMoving is true player is unable to move.
			enemiesMoving = true;
			
			//Wait for turnDelay seconds, defaults to .1 (100 ms).
			yield return new WaitForSeconds(turnDelay);
			
			//If there are no enemies spawned (IE in first level):
			if (enemies.Count == 0) 
			{
				//Wait for turnDelay seconds between moves, replaces delay caused by enemies moving when there are none.
				yield return new WaitForSeconds(turnDelay);
			}
			
			//Loop through List of Enemy objects.
			for (int i = 0; i < enemies.Count; i++)
			{
                enemies[i].UpdateTarget();

				//Call the MoveEnemy function of Enemy at index i in the enemies List.
				enemies[i].MoveEnemy ();
				
				//Wait for Enemy's moveTime before moving next Enemy, 
				yield return new WaitForSeconds(enemies[i].moveTime);
			}
			//Once Enemies are done moving, set playersTurn to 0 so player 0 can move.
			playersTurn = 0;
			
			//Enemies are done moving, set enemiesMoving to false.
			enemiesMoving = false;
		}

        public int GetTotalCurrentFoodPoints()
        {
            int currentFood = 0;

            Player[] players = GameObject.FindObjectsOfType<Player>();

            for (int playerIdx = 0; playerIdx < players.Length; playerIdx++)
            {
                currentFood += players[playerIdx].GetFood();
            }

            return currentFood;
        }

        public int GetCurrentFoodPoints(int playerId)
        {
            return playerFoodPoints[playerId];
        }

        public void SetPlayerFoodPoints(int playerId, int food)
        {
            playerFoodPoints[playerId] = food;
        }

        public void PlayerMoved(int playerId, int xDir, int yDir)
        {
            SetNextPlayersTurn();

            if (NetworkManager.Instance.IsActive() && NetworkManager.Instance.IsHost())
            {
                Vector3 move = new Vector3();
                move.x = xDir;
                move.y = yDir;

                NetworkManager.Instance.BroadcastPlayerMove(playerId, move);
            }
        }

        public void SetNextPlayersTurn()
        {
            playersTurn++;

            // if all players have moved set to -1
            if (playersTurn >= numPlayers)
            {
                playersTurn = -1;
            }
        }

        public BoardManager GetBoardManager()
        {
            return boardScript;
        }

        public void SetLevel(int level)
        {
            this.level = level;
        }

        public void SetClientReadyToStart(bool ready)
        {
            clientReadyToStart = ready;
        }

        public void RecievedPlayerMove(int playerId, Vector3 move)
        {
            pendingMovementMutex.WaitOne();
            PendingMove pendingMove;
            pendingMove.playerId = playerId;
            pendingMove.movement = move;
            pendingMovements.Enqueue(pendingMove);
            pendingMovementMutex.ReleaseMutex();
        }
    }
}

