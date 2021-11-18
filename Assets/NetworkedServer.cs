using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using UnityEngine.UIElements;

public class NetworkedServer : MonoBehaviour
{
    int maxConnections = 1000;
    int reliableChannelID;
    int unreliableChannelID;
    int hostID;
    int socketPort = 5491;

    LinkedList<PlayerAccount> playerAccounts;

    PlayerAccount temp1 = null;
    PlayerAccount temp2 = null;
    PlayerAccount temp3 = null;

    GameRoom room1 = null;
    GameRoom room2 = null;
    GameRoom room3 = null;

    bool room1InUse = false;
    bool room2InUse = false;
    bool room3InUse = false;

    // Start is called before the first frame update
    void Start()
    { 
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliableChannelID = config.AddChannel(QosType.Reliable);
        unreliableChannelID = config.AddChannel(QosType.Unreliable);
        HostTopology topology = new HostTopology(config, maxConnections);
        hostID = NetworkTransport.AddHost(topology, socketPort, null);

        playerAccounts = new LinkedList<PlayerAccount>();
    }

    // Update is called once per frame
    void Update()
    {
        int recHostID;
        int recConnectionID;
        int recChannelID;
        byte[] recBuffer = new byte[1024];
        int bufferSize = 1024;
        int dataSize;
        byte error = 0;

        NetworkEventType recNetworkEvent = NetworkTransport.Receive(out recHostID, out recConnectionID, out recChannelID, recBuffer, bufferSize, out dataSize, out error);

        switch (recNetworkEvent)
        {
            case NetworkEventType.Nothing:
                break;
            case NetworkEventType.ConnectEvent:
                Debug.Log("Connection, " + recConnectionID);
                break;
            case NetworkEventType.DataEvent:
                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
                ProcessRecievedMsg(msg, recConnectionID);
                break;
            case NetworkEventType.DisconnectEvent:
                Debug.Log("Disconnection, " + recConnectionID);
                break;
        }
    }

    public void SendMessageToClient(string msg, int id)
    {
        byte error = 0;
        byte[] buffer = Encoding.Unicode.GetBytes(msg);
        NetworkTransport.Send(hostID, id, reliableChannelID, buffer, msg.Length * sizeof(char), out error);
    }

    private void ProcessRecievedMsg(string msg, int id)
    {
        Debug.Log("msg recieved = " + msg + ".  connection id = " + id);

        //Split message by comma, allowing signifiers to be read.
        string[] csv = msg.Split(',');

        int stateSignifier = int.Parse(csv[0]);

        //Create new account.
        if(stateSignifier == ClientToServerStateSignifiers.Account)
        {
            int accountSignifier = int.Parse(csv[1]);

            if(accountSignifier == ClientToServerAccountSignifiers.CreateAccount)
            {
                Debug.Log("Create Account");

                string n = csv[2];
                string p = csv[3];
                string gameID = csv[4];
                bool nameInUse = false;

                foreach(PlayerAccount pa in playerAccounts)
                {
                    if (pa.username == n)
                    {
                        nameInUse = true;
                        break;
                    }
                }

                if(nameInUse)
                {
                    //Name in use, account cannot be created.
                    SendMessageToClient(ServerToClientStateSignifiers.Account + "," + ServerToClientAccountSignifiers.AccountCreationFailed + "", id); 
                }

                else if(!nameInUse)
                {
                    //Name not in use, create account & add to list.
                    PlayerAccount newAccount = new PlayerAccount(n, p, int.Parse(gameID), id);
                    playerAccounts.AddLast(newAccount);
                    SendMessageToClient(ServerToClientStateSignifiers.Account + "," + ServerToClientAccountSignifiers.AccountCreationComplete + "", id);
                }
            }

            else if(accountSignifier == ClientToServerAccountSignifiers.Login)
            {
                Debug.Log("Login to account");
                string n = csv[2]; //name
                string p = csv[3]; //password
                string g = csv[4]; //game ID
                bool accountFound = false;

                PlayerAccount thisPlayer = null;

                //Check for login info match.
                foreach(PlayerAccount pa in playerAccounts)
                {
                    //Check for username match.
                    if(pa.username == n)
                    {
                        //Check for password match.
                        if(pa.password == p)
                        {
                            //Username and password match. Login
                            accountFound = true;
                            thisPlayer = pa;
                            break;
                        }
                    }
                }

                //Account found, proceed to login to game room specified.
                if(accountFound)
                {
                    Debug.Log("Account Found");
                    SendMessageToClient(ServerToClientStateSignifiers.Account + "," + ServerToClientAccountSignifiers.LoginComplete + "", id);

                    if(g == "1")
                    {
                        if(temp1 == null)
                        {
                            //Player 1 connects. Wait for player 2.
                            temp1 = thisPlayer;
                            thisPlayer = null;
                        }
                        else if(temp1 != null && thisPlayer != null && !room1InUse)
                        {
                            //Player 2 connects. Begin game.
                            Debug.Log(temp1.username + " " + thisPlayer.username);

                            room1 = new GameRoom(temp1, thisPlayer);
                            //Initialize game for P1 and P2
                            SendMessageToClient(ServerToClientStateSignifiers.Game + "," + ServerToClientGameSignifiers.GameInitialize + "," + thisPlayer.username, temp1.playerID);
                            SendMessageToClient(ServerToClientStateSignifiers.Game + "," + ServerToClientGameSignifiers.GameInitialize + "," + temp1.username, thisPlayer.playerID);
                            //Notify P1 that it's their turn.
                            SendMessageToClient(ServerToClientStateSignifiers.Game + "," + ServerToClientGameSignifiers.CurrentTurn + "," + room1.TopLeft.status + "," + room1.TopMiddle.status + "," + room1.TopRight.status + "," + room1.MiddleLeft.status + "," + room1.Middle.status + "," + room1.MiddleRight.status + "," + room1.BottomLeft.status + "," + room1.BottomMiddle.status + "," + room1.BottomRight.status + "", temp1.playerID);
                            temp1 = null;
                            thisPlayer = null;
                            room1InUse = true;
                        }
                        else if(temp1 != null && room1InUse)
                        {
                            //Player is spectator for room 1.
                        }
                    }
                    else if (g == "2")
                    {
                        //Implementation for room 2 setup.
                    }
                    else if (g == "3")
                    {
                        //Implementation for room 3 setup.
                    }
                }

                //Account not found, login failed.
                else if (!accountFound)
                {
                    Debug.Log("Account Not Found");
                }
            }
        }
        else if(stateSignifier == ClientToServerStateSignifiers.Game)
        {
            int gameSignifier = int.Parse(csv[1]);

            if(gameSignifier == ClientToServerGameSignifiers.ChoiceMade)
            {
                int choice = int.Parse(csv[2]);

                if(room1InUse) //Check room 1.
                {
                    //Player1 (X)
                    if (room1.P1Turn && room1.player1.playerID == id)
                    {
                        GameLoop(choice, room1, room1.player1);
                    }
                    else if (room1.P1Turn == false && room1.player2.playerID == id) //Player2 (O)
                    {
                        GameLoop(choice, room1, room1.player2);
                    }
                }
                if(room2InUse) //Check room 2.
                {
                    //Player1 (X)
                    if (room2.P1Turn && room2.player1.playerID == id)
                    {
                        GameLoop(choice, room2, room2.player1);
                    }
                    else if (room2.P1Turn == false && room2.player2.playerID == id) //Player2 (O)
                    {
                        GameLoop(choice, room2, room2.player2);
                    }
                }
                if(room3InUse) //Check room 3.
                {
                    //Player1 (X)
                    if (room3.P1Turn && room3.player1.playerID == id)
                    {
                        GameLoop(choice, room3, room3.player1);
                    }
                    else if (room3.P1Turn == false && room3.player2.playerID == id) //Player2 (O)
                    {
                        GameLoop(choice, room3, room3.player2);
                    }
                }
            }
        }
    }

    //Runs game logic.
    public void GameLoop(int choice, GameRoom room, PlayerAccount player)
    {
        Debug.Log("Player " + player.username + " chose square " + choice);
        //Update game board with player choice
        UpdateBoard(room, choice, 1);
        //Update player 1's UI
        SendMessageToClient(ServerToClientStateSignifiers.Game + "," + ServerToClientGameSignifiers.RefreshUI + "," + room.TopLeft.status + "," + room.TopMiddle.status + "," + room.TopRight.status + "," + room.MiddleLeft.status + "," + room.Middle.status + "," + room.MiddleRight.status + "," + room.BottomLeft.status + "," + room.BottomMiddle.status + "," + room.BottomRight.status + "", player.playerID);
        //Win Check
        int winCheck = room.CheckGameWin();
        if (winCheck != 0) //If win condition met
        {
            if (winCheck == 1) //Player 1 win
            {
                SendMessageToClient(ServerToClientStateSignifiers.Game + "," + ServerToClientGameSignifiers.YouWon + "," + room.player1.username + " won!", room.player1.playerID);
                SendMessageToClient(ServerToClientStateSignifiers.Game + "," + ServerToClientGameSignifiers.OpponentWon + "," + room.player1.username + " won!", room.player2.playerID);
            }
            else if (winCheck == 2) //Player 2 win
            {
                SendMessageToClient(ServerToClientStateSignifiers.Game + "," + ServerToClientGameSignifiers.OpponentWon + "," + room.player2.username + " won!", room.player1.playerID);
                SendMessageToClient(ServerToClientStateSignifiers.Game + "," + ServerToClientGameSignifiers.YouWon + "," + room.player2.username + " won!", room.player2.playerID);
            }
        }
        else //No win, continue game
        {
            if(player.playerID == room.player1.playerID)
            {
                ChangeTurn(room.player2, room);
            }
            else if(player.playerID == room.player2.playerID)
            {
                ChangeTurn(room.player1, room);
            }
        }
    }

    //Resets game to default settings
    public void ResetGame(GameRoom room)
    {
        //Reset game to base values
        room.P1Turn = true;
        room.TopLeft.status = 0;
        room.TopMiddle.status = 0;
        room.TopRight.status = 0;
        room.MiddleLeft.status = 0;
        room.Middle.status = 0;
        room.MiddleRight.status = 0;
        room.BottomLeft.status = 0;
        room.BottomMiddle.status = 0;
        room.BottomRight.status = 0;

        SendMessageToClient(ServerToClientStateSignifiers.Game + "," + ServerToClientGameSignifiers.RefreshUI + "," + room.TopLeft.status + "," + room.TopMiddle.status + "," + room.TopRight.status + "," + room.MiddleLeft.status + "," + room.Middle.status + "," + room.MiddleRight.status + "," + room.BottomLeft.status + "," + room.BottomMiddle.status + "," + room.BottomRight.status + "", room.player1.playerID);
        SendMessageToClient(ServerToClientStateSignifiers.Game + "," + ServerToClientGameSignifiers.RefreshUI + "," + room.TopLeft.status + "," + room.TopMiddle.status + "," + room.TopRight.status + "," + room.MiddleLeft.status + "," + room.Middle.status + "," + room.MiddleRight.status + "," + room.BottomLeft.status + "," + room.BottomMiddle.status + "," + room.BottomRight.status + "", room.player2.playerID);
    }

    public void UpdateBoard(GameRoom room, int choice, int playerNumber)
    {
        if (choice == 1) //Top Left
        {
            room.TopLeft.status = playerNumber;
        }
        else if (choice == 2) //Top Middle
        {
            room.TopMiddle.status = playerNumber;
        }
        else if (choice == 3) //Top Right
        {
            room.TopRight.status = playerNumber;
        }
        else if (choice == 4) //Middle Left
        {
            room.MiddleLeft.status = playerNumber;
        }
        else if (choice == 5) //Middle
        {
            room.Middle.status = playerNumber;
        }
        else if (choice == 6) //Middle Right
        {
            room.MiddleRight.status = playerNumber;
        }
        else if (choice == 7) //Bottom Left
        {
            room.BottomLeft.status = playerNumber;
        }
        else if (choice == 8) //Bottom Middle
        {
            room.BottomMiddle.status = playerNumber;
        }
        else if (choice == 9) //BottomRight
        {
            room.BottomRight.status = playerNumber;
        }
    }

    //Alternate player turns.
    public void ChangeTurn(PlayerAccount swapTo, GameRoom room)
    {
        SendMessageToClient(ServerToClientStateSignifiers.Game + "," + ServerToClientGameSignifiers.CurrentTurn + "," + room.TopLeft.status + "," + room.TopMiddle.status + "," + room.TopRight.status + "," + room.MiddleLeft.status + "," + room.Middle.status + "," + room.MiddleRight.status + "," + room.BottomLeft.status + "," + room.BottomMiddle.status + "," + room.BottomRight.status + "", swapTo.playerID);
        room.P1Turn = !room.P1Turn;
    }
}

public class PlayerAccount
{
    public string username;
    public string password;
    public int gameID;
    public int playerID;

    public PlayerAccount(string name, string pass, int gameIDNum, int playerIDNum)
    {
        username = name;
        password = pass;
        gameID = gameIDNum;
        playerID = playerIDNum;
    }
}

public class GameRoom
{
    public PlayerAccount player1 = null;
    public PlayerAccount player2 = null;

    public bool P1Turn;

    public GameTile TopLeft;
    public GameTile TopMiddle;
    public GameTile TopRight;
    public GameTile MiddleLeft;
    public GameTile Middle;
    public GameTile MiddleRight;
    public GameTile BottomLeft;
    public GameTile BottomMiddle;
    public GameTile BottomRight;

    public GameRoom(PlayerAccount xPlayer, PlayerAccount oPlayer)
    {
        player1 = xPlayer;

        player2 = oPlayer;

        P1Turn = true;

        TopLeft = new GameTile(0);
        TopMiddle = new GameTile(0);
        TopRight = new GameTile(0);
        MiddleLeft = new GameTile(0);
        Middle = new GameTile(0);
        MiddleRight = new GameTile(0);
        BottomLeft = new GameTile(0);
        BottomMiddle = new GameTile(0);
        BottomRight = new GameTile(0);
    }

    public int CheckGameWin()
    {
        //Top Row Win
        if(TopLeft.status == TopMiddle.status && TopMiddle.status == TopRight.status && TopMiddle.status != 0)
        {
            if(TopMiddle.status == 1)
            {
                return 1;
            }
            else if (TopMiddle.status == 2)
            {
                return 2;
            }
        }
        //Middle Row Win
        if (MiddleLeft.status == Middle.status && Middle.status == MiddleRight.status && Middle.status != 0)
        {
            if (Middle.status == 1)
            {
                return 1;
            }
            else if (Middle.status == 2)
            {
                return 2;
            }
        }
        //Bottom Row Win
        if (BottomLeft.status == BottomMiddle.status && BottomMiddle.status == BottomRight.status && BottomMiddle.status != 0)
        {
            if (BottomMiddle.status == 1)
            {
                return 1;
            }
            else if (BottomMiddle.status == 2)
            {
                return 2;
            }
        }
        //Left Column Win
        if (TopLeft.status == MiddleLeft.status && MiddleLeft.status == BottomLeft.status && MiddleLeft.status != 0)
        {
            if (MiddleLeft.status == 1)
            {
                return 1;
            }
            else if (MiddleLeft.status == 2)
            {
                return 2;
            }
        }
        //Middle Column Win
        if (TopMiddle.status == Middle.status && Middle.status == BottomMiddle.status && Middle.status != 0)
        {
            if (Middle.status == 1)
            {
                return 1;
            }
            else if (Middle.status == 2)
            {
                return 2;
            }
        }
        //Right Column Win
        if (TopRight.status == MiddleRight.status && MiddleRight.status == BottomRight.status && MiddleRight.status != 0)
        {
            if (MiddleRight.status == 1)
            {
                return 1;
            }
            else if (MiddleRight.status == 2)
            {
                return 2;
            }
        }
        //Left Diagonal Win
        if (TopLeft.status == Middle.status && Middle.status == BottomRight.status && Middle.status != 0)
        {
            if (Middle.status == 1)
            {
                return 1;
            }
            else if (Middle.status == 2)
            {
                return 2;
            }
        }
        //Right Diagonal Win
        if (TopRight.status == Middle.status && Middle.status == BottomLeft.status && Middle.status != 0)
        {
            if (Middle.status == 1)
            {
                return 1;
            }
            else if (Middle.status == 2)
            {
                return 2;
            }
        }
        //No Win
        return 0;
    }
}

public class GameTile
{
    //Status
    //0 = Unclaimed
    //1 = X
    //2 = O
    public int status;
    public GameTile(int tileStatus)
    {
        status = tileStatus;
    }
}

public static class ClientToServerStateSignifiers
{
    public const int Account = 1;

    public const int Game = 2;

    public const int Spectate = 3;

    public const int Other = 9;
}

public static class ClientToServerAccountSignifiers
{
    public const int CreateAccount = 1;

    public const int Login = 2;
}

public static class ClientToServerGameSignifiers
{
    public const int ChoiceMade = 1;
}

public static class ServerToClientStateSignifiers
{
    public const int Account = 1;

    public const int Game = 2;
}

public static class ServerToClientAccountSignifiers
{
    public const int LoginComplete = 1;

    public const int LoginFailed = 2;

    public const int AccountCreationComplete = 3;

    public const int AccountCreationFailed = 4;
}

public static class ServerToClientGameSignifiers
{
    public const int CurrentTurn = 1;

    public const int YouWon = 2;

    public const int OpponentWon = 3;

    public const int RefreshUI = 4;

    public const int GameInitialize = 9;

}

