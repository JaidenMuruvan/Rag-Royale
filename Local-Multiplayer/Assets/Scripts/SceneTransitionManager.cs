using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionManager : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float roundWinDisplayTime  = 2.5f; 
    [SerializeField] private float matchWinDisplayTime  = 4f; 



    private void Start()
    {
      
        RoundManager roundManager = FindFirstObjectByType<RoundManager>();
        if (roundManager == null)
        {
            Debug.LogWarning("[SceneTransitionManager] theres no RoundManager here boii.");
            return;
        }

        roundManager.OnRoundWon.AddListener(OnRoundWon);
        roundManager.OnMatchWon.AddListener(OnMatchWon);
    }



    private void OnRoundWon(int winnerID)
    {
    
    }

    private void OnMatchWon(int winnerID)
    {
        StartCoroutine(LoadMatchResult(winnerID));
    }

    


    public void LoadNextRoundScene(int nextRound)
    {
        StartCoroutine(LoadRoundScene(nextRound));
    }

    private IEnumerator LoadRoundScene(int roundNumber)
    {
        yield return new WaitForSeconds(roundWinDisplayTime);

        if (roundNumber < 1 || roundNumber >= MatchData.RoundScenes.Length)
        {
          
            yield break;
        }

        SceneManager.LoadScene(MatchData.RoundScenes[roundNumber]);
    }

    private IEnumerator LoadMatchResult(int winnerID)
    {
        yield return new WaitForSeconds(matchWinDisplayTime);

       
      SceneManager.LoadScene(MatchData.MainMenuScene);

        // SceneManager.LoadScene(target);

       
        if (MatchData.Instance != null)
        {
            MatchData.Instance.ResetMatch();
            Destroy(MatchData.Instance.gameObject);
        }
    }
}