using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CardManager : NetworkBehaviour
{
    public DeckData deckData;
    private List<Cards> runtimeDeck;

    void Start()
    {
        runtimeDeck = new List<Cards>(deckData.cards);
        Shuffle();
    }

    void Shuffle()
    {
        for (int i = runtimeDeck.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (runtimeDeck[i], runtimeDeck[j]) = (runtimeDeck[j], runtimeDeck[i]);
        }
    }

    public Cards Draw()
    {
        if (runtimeDeck.Count < 10)
        {
            runtimeDeck = new List<Cards>(deckData.cards);
            Shuffle();
            Debug.Log("Deck reshuffled!");
        }
        
        Cards card = runtimeDeck[0];
        runtimeDeck.RemoveAt(0);
        return card;
    }
    
    public int GetDeckCount()
    {
        return runtimeDeck.Count;
    }
    
    public void ResetDeck()
    {
        runtimeDeck = new List<Cards>(deckData.cards);
        Shuffle();
    }

    public Cards GetCard(Cards.Suit suit, Cards.Rank rank)
    {
        foreach (Cards card in deckData.cards)
        {
            if (card.suit == suit && card.rank == rank) return card;
        }

        return null;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void DrawCardServerRpc(ulong clientId)
    {
        Cards card = Draw();
        DrawCardClientRPC(card.suit, card.rank, clientId);
    }

    [Rpc(SendTo.ClientsAndHost)]
    public void DrawCardClientRPC(Cards.Suit suit, Cards.Rank rank, ulong clientId)
    {
        if (NetworkManager.Singleton.LocalClientId != clientId) return;
        Cards card = GetCard(suit, rank);
        GameManager.Instance.ReceiveCard(card);
    }
}
