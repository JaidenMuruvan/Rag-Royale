using UnityEngine;

/// <summary>
/// match data persistent singleton that carries match state, round wins across all the round scenes
/// </summary> <summary>
/// 
/// </summary>

public class MatchData : MonoBehaviour
{
    public static MatchData Instance { get; private set; }

    //---match statess---

    public int P1RoundWins { get; set; }
    public int P2RoundWins { get; set; }
    public int CurrentRound { get; set; } = 1;
    public int RoundsToWin { get; set; } = 2;



    //round 2 things carried
    public int P1NeedleCount { get; set; }
    public int P2NeedleCount { get; set; }


    //----- registerr scene w names

    public static readonly string[] RoundScenes =
    {
         "",    
        "Round1",
        "Round2",
        "Round3"
    };

    public static readonly string MainMenuScene = "MainMenu";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    ///
    /// <summary>
    /// this to fully reset before new round...

    public void ResetMatch()
    {
        P1RoundWins = 0;
        P2RoundWins = 0;
        CurrentRound = 1;
        P1NeedleCount = 0;
        P2NeedleCount = 0;

    }


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }
}
