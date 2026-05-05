using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu]
public class Cards : ScriptableObject
{
    public enum Suit { Hearts, Spades, Diamonds, Clubs }
    public enum Rank { Ace, Two, Three, Four, Five, Six, Seven, Eight, Nine, Ten, Jack, Queen, King }

    public Suit suit;
    public Rank rank;
    public Sprite sprite;

    public int GetBlackjackValue()
    {
        if (rank == Rank.Ace) return 11;
        if (rank >= Rank.Jack) return 10;
        return (int)rank + 1;
    }
}
