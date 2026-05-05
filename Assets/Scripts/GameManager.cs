using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.SceneManagement;


public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }
    
    public CardManager cardManager;
    public GameObject cardPrefab;
    public Transform playerHandArea;
    public Transform dealerHandArea;
    private GameObject dealerHiddenCard;
    public Sprite cardBackSprite;
    public GameObject newGameButton;
    private Vector2 playerHandStartPos;
    private Vector2 dealerHandStartPos;
    public GameObject gameOverScreen;
    public TMP_Text balanceText;
    public TMP_Text betText;
    public GameObject hitAndStandVert;
    public GameObject pauseMenu;
    private bool autoStandOn21 = true;
    public GameObject betVert;
    public GameObject decisionVert;
    [SerializeField] private int cardsRemaining;
    public TMP_Text resultText;
    public GameObject deckObject;
    private bool playerBusted;
    private bool turnOver = false;
    
    private NetworkVariable<int> currentPlayerTurn = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> playersReady = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> playersStood = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    private NetworkVariable<int> player1Balance = new NetworkVariable<int>(500, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> player2Balance = new NetworkVariable<int>(500, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> player3Balance = new NetworkVariable<int>(500, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> player4Balance = new NetworkVariable<int>(500, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    public int balance = 500;
    private int currentBet = 0;
    
    public List<Cards> playerHand = new List<Cards>();
    public List<Cards> dealerHand = new List<Cards>();
    
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        currentPlayerTurn.OnValueChanged += OnTurnChanged;
        balanceText.text = "$" + balance;
        playerHandStartPos = playerHandArea.GetComponent<RectTransform>().anchoredPosition;
        dealerHandStartPos = dealerHandArea.GetComponent<RectTransform>().anchoredPosition;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }
    }

    void OnTurnChanged(int previous, int current)
    {
        bool isMyTurn = current == (int)NetworkManager.Singleton.LocalClientId;
        hitAndStandVert.SetActive(isMyTurn);
    }
    
    public void AddBet(int amount)
    {
        if (currentBet + amount > balance)
        {
            Debug.Log("Not enough balance!");
            return;
        }
        currentBet += amount;
        betText.text = "$" + currentBet;
        Debug.Log("Current bet: $" + currentBet);
    }
    
    public void RemoveBet()
    {
        currentBet = 0;
        betText.text = "$" + currentBet;
    }
    
    public void AllIn()
    {
        currentBet = balance;
        betText.text = "Bet: $" + currentBet;
    }
    
    public void PlaceBetAndDeal()
    {
        if (currentBet == 0)
        {
            StartCoroutine(ShowMessage("Place a bet first", 2f));
            return;
        }
        balance -= currentBet;
        balanceText.text = "$" + balance;
        betText.text = "Bet: $" + currentBet;
        deckObject.SetActive(true);
        //hitAndStandVert.SetActive(true);
        betVert.SetActive(false);
        decisionVert.SetActive(false);
        PlayersReadyServerRpc();
    }
    
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void PlayersReadyServerRpc()
    {
        playersReady.Value++;
        if (playersReady.Value >= NetworkManager.Singleton.ConnectedClientsIds.Count)
        {
            playersReady.Value = 0;
            DealInitialCardsServerRpc();
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void DealInitialCardsServerRpc()
    {
        foreach (ulong clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            cardManager.DrawCardServerRpc(clientId);
            cardManager.DrawCardServerRpc(clientId);
        }

        Cards dealerCard1 = cardManager.Draw();
        Cards dealerCard2 = cardManager.Draw();
        DealDealerCardsClientRpc(dealerCard1.suit, dealerCard1.rank, dealerCard2.suit, dealerCard2.rank);
        currentPlayerTurn.Value = -1;
        currentPlayerTurn.Value = (int)NetworkManager.Singleton.ConnectedClientsIds[0];
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void HitServerRpc(ulong clientId)
    {
        if (currentPlayerTurn.Value != (int)clientId) return;
        cardManager.DrawCardServerRpc(clientId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void StandServerRpc(ulong clientId)
    {
        if (currentPlayerTurn.Value != (int)clientId) return;
        playersStood.Value++;
        int connectedPlayers = NetworkManager.Singleton.ConnectedClientsIds.Count;
        if (playersStood.Value >= connectedPlayers)
        {
            playersStood.Value = 0;
            RunDealerLogicServerRpc();
        }
        else
        {
            currentPlayerTurn.Value++;
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void DealDealerCardsClientRpc(Cards.Suit suit1, Cards.Rank rank1, Cards.Suit suit2, Cards.Rank rank2)
    {
        Debug.Log("DealDealerCardsClientRpc called");
        
        Cards card1 = cardManager.GetCard(suit1, rank1);
        Cards card2 = cardManager.GetCard(suit2, rank2);
        dealerHand.Add(card1);
        dealerHiddenCard = SpawnCard(card1, dealerHandArea, dealerHand);
        dealerHiddenCard.GetComponent<Image>().sprite = cardBackSprite;
        
        dealerHand.Add(card2);
        SpawnCard(card2, dealerHandArea, dealerHand);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RunDealerLogicServerRpc()
    {
        List<Cards.Suit> suits = new List<Cards.Suit>();
        List<Cards.Rank> ranks = new List<Cards.Rank>();

        while (CalculateHandValue(dealerHand) < 17)
        {
            Cards card = cardManager.Draw();
            dealerHand.Add(card);
            suits.Add(card.suit);
            ranks.Add(card.rank);
        }
        
        SyncDealerCardsClientRpc(suits.ToArray(), ranks.ToArray());
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void SyncDealerCardsClientRpc(Cards.Suit[] suits, Cards.Rank[] ranks)
    {
        StartCoroutine(DealerPlay(suits, ranks));
    }
    
    IEnumerator DealerPlay(Cards.Suit[] suits, Cards.Rank[] ranks)
    {
        dealerHiddenCard.GetComponent<Image>().sprite = dealerHand[0].sprite;
        yield return new WaitForSeconds(0.5f);

        for (int i = 0; i < ranks.Length; i++)
        {
            Cards card = cardManager.GetCard(suits[i], ranks[i]);
            dealerHand.Add(card);
            SpawnCard(card, dealerHandArea, dealerHand);
            yield return new WaitForSeconds(0.6f);
        }

        int playerValue = CalculateHandValue(playerHand);
        int dealerValue = CalculateHandValue(dealerHand);


        if (playerBusted)
        {
            ShowResult("Bust! You lose");
            PayOut(false, false);
        }
        else if (dealerValue > 21)
        {
            ShowResult("Dealer busts! You Win!");
            PayOut(true, false);
        }
        else if (playerValue > dealerValue)
        {
            ShowResult("You Win!");
            PayOut(true, false);
        }
        else if (dealerValue == playerValue)
        {
            ShowResult("Push - It's a tie!");
            PayOut(false, true);
        }
        else if (dealerValue > playerValue)
        {
            ShowResult("Dealer wins!");
            PayOut(false, false);
        }
        
        if (balance > 0) newGameButton.SetActive(true);
    }
    
    public void Hit()
    {
        HitServerRpc(NetworkManager.Singleton.LocalClientId);
    }

    public void Stand()
    {
        turnOver = true;
        StandServerRpc(NetworkManager.Singleton.LocalClientId);
    }
    
    public void ReceiveCard(Cards card)
    {
        if (turnOver) return;
        
        playerHand.Add(card);
        SpawnCard(card, playerHandArea, playerHand);
        
        int value = CalculateHandValue(playerHand);

        if (value > 21)
        {
            ShowResult("Bust You lose!");
            dealerHiddenCard.GetComponent<Image>().sprite = dealerHand[0].sprite;
            PayOut(false, false);
            turnOver = true;
            playerBusted = true;
            StandServerRpc(NetworkManager.Singleton.LocalClientId);
            return;
        }

        if (playerHand.Count == 5 || autoStandOn21 && value == 21)
        {
            turnOver = true;
            StandServerRpc(NetworkManager.Singleton.LocalClientId);
        }

        if (playerHand.Count == 2 && CalculateHandValue(playerHand) == 21)
        {
            ShowResult("Blackjack! You win!");
            PayOut(true, false, true);
            turnOver = true;
            StandServerRpc(NetworkManager.Singleton.LocalClientId);
            return;
        }
    }

    int CalculateHandValue(List<Cards> hand)
    {
        int total = 0;
        int aces = 0;

        foreach (Cards card in hand)
        {
            total += card.GetBlackjackValue();
            if (card.rank == Cards.Rank.Ace) aces++;
        }

        while (total > 21 && aces > 0)
        {
            total -= 10;
            aces--;
        }

        return total;
    }
    
    void PayOut(bool playerWon, bool push, bool blackjack = false)
    {
        if (push) balance += currentBet;
        else if (blackjack) balance += Mathf.RoundToInt(currentBet * 2.5f);
        else if (playerWon) balance += currentBet * 2;
        currentBet = 0;
        
        balanceText.text = "$" + balance;
        betText.text = "$" + currentBet;

        betVert.SetActive(false);
        hitAndStandVert.SetActive(false);
        decisionVert.SetActive(false);


        if (balance == 0) StartCoroutine(GameOverDelay());
    }

    void ShowResult(string message)
    {
        resultText.text = message;
        resultText.gameObject.SetActive(true);
    }
    
    public void NewGame()
    {
        turnOver = false;
        playerBusted = false;
        playerHand.Clear();
        dealerHand.Clear();
        foreach (Transform child in playerHandArea) Destroy(child.gameObject);
        foreach (Transform child in dealerHandArea) Destroy(child.gameObject);
        dealerHiddenCard = null;
        deckObject.SetActive(false);
        hitAndStandVert.SetActive(false);
        betVert.SetActive(true);
        decisionVert.SetActive(true);
        newGameButton.SetActive(false);
        playerHandArea.GetComponent<RectTransform>().anchoredPosition = playerHandStartPos;
        dealerHandArea.GetComponent<RectTransform>().anchoredPosition = dealerHandStartPos;
        resultText.gameObject.SetActive(false);
    }
    
    public void CompleteNewGame()
    {
        gameOverScreen.SetActive(false);
        balance = 500;
        balanceText.text = "$" + balance;
        NewGame();
        cardManager.ResetDeck();
    }
    
    public void TogglePause()
    {
        pauseMenu.SetActive(!pauseMenu.activeSelf);
    }
    
    public void Resume()
    {
        TogglePause();
    }
    
    public void SetAutoStand(bool value)
    {
        autoStandOn21 = value;
    }
    
    public void Quit()
    {
        Application.Quit();
    }
    
    public void MainMenu()
    {
        SceneManager.LoadScene("Main Menu");
    }
    
    IEnumerator ShowMessage(string message, float duration)
    {
        resultText.gameObject.SetActive(true);
        resultText.text = message;
        yield return new WaitForSeconds(duration);
        resultText.gameObject.SetActive(false);
    }
    
    IEnumerator GameOverDelay()
    {
        newGameButton.SetActive(false);
        yield return new WaitForSeconds(2f);
        gameOverScreen.SetActive(true);
    }

    GameObject SpawnCard(Cards card, Transform handArea, List<Cards> hand)
    {
        GameObject cardObject = Instantiate(cardPrefab, handArea);
        RectTransform rt = cardObject.GetComponent<RectTransform>();
        Vector2 targetPos = new Vector2(hand.Count * 200f, 0);
        rt.anchoredPosition = deckObject.GetComponent<RectTransform>().anchoredPosition;
        cardObject.GetComponent<Image>().sprite = card.sprite;
        StartCoroutine(DealAnimation(cardObject, targetPos, 0.6f));
        RectTransform handAreaRT = handArea.GetComponent<RectTransform>();
        handAreaRT.anchoredPosition -= new Vector2(100f, 0);
        return cardObject;
    }
    
    IEnumerator DealAnimation(GameObject card, Vector2 targetPos, float duration)
    {
        RectTransform rt = card.GetComponent<RectTransform>();
        Vector2 startPos = deckObject.GetComponent<RectTransform>().anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            rt.anchoredPosition = Vector2.Lerp(startPos, targetPos, elapsed / duration);
            yield return null;
        }

        rt.anchoredPosition = targetPos;
    }
}
