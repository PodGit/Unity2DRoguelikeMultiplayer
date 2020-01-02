using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;

namespace Completed
{
    public class Lobby : MonoBehaviour
    {
        public int playerPanelSpacing = 100;

        // Start is called before the first frame update
        void Start()
        {
            InitialisePanels();
        }

        // Update is called once per frame
        void Update()
        {

        }

        void InitialisePanels()
        {
            for (int playerIdx = 0; playerIdx < 4 /*GameManager.instance.numPlayers*/; playerIdx++)
            {
                // skip 0, spawned in scene
                if (playerIdx > 0)
                {
                    CreatePanel(playerIdx);
                }

                NetworkManager.Peer currentPeer = NetworkManager.Instance.GetPeer(playerIdx);
            }
        }

        void CreatePanel(int index)
        {
            GameObject container = GameObject.Find("LobbyPlayerPanelContainer");
            GameObject playerPanel = GameObject.Find("LobbyPlayerPanel");
            GameObject newPanel = Instantiate(playerPanel, playerPanel.transform, true);

            Vector3 position = newPanel.transform.position;
            position.x += playerPanelSpacing * index;
            newPanel.transform.position = position;
            newPanel.name = "LobbyPlayerPanel" + index;
            newPanel.transform.SetParent(container.transform);

            Transform numPanelTextTransform = newPanel.transform.Find("LobbyPlayerNumText");
            UnityEngine.UI.Text numPaneltext = numPanelTextTransform.GetComponent<UnityEngine.UI.Text>();
            numPaneltext.text = "Player " + (index + 1);
        }
    }
}
