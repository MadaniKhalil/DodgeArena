using UnityEngine;

namespace DodgeArena
{
    /// <summary>
    /// Shows the score of one agent in the top-left corner of the Game view.
    /// Visual only: it reads values from the agent and changes nothing.
    /// </summary>
    public class ScoreDisplay : MonoBehaviour
    {
        [Tooltip("The agent whose score is shown.")]
        public DodgeAgent agent;

        private void OnGUI()
        {
            if (agent == null)
            {
                return;
            }

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 28;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = Color.white;

            string text = "Targets: " + agent.TargetsThisEpisode
                        + "     Best: " + agent.BestTargets
                        + "     Episode: " + (agent.CompletedEpisodes + 1);

            GUI.Label(new Rect(20, 20, 800, 50), text, style);
        }
    }
}
