using UnityEngine;
using UnityEngine.SceneManagement;

// Tela de vitória / game over. Não precisa de nada na cena: ela se cria sozinha.
// Uso: FimDeJogo.Vitoria();  ou  FimDeJogo.Derrota();
public class FimDeJogo : MonoBehaviour
{
    static FimDeJogo instance;

    // true enquanto a tela de vitória / game over estiver aberta
    public static bool Ativo { get { return instance != null && instance.ativo; } }

    bool ativo;
    bool venceu;

    public static void Vitoria() { Mostrar(true); }
    public static void Derrota() { Mostrar(false); }

    static void Mostrar(bool vitoria)
    {
        if (instance != null && instance.ativo) return;   // só mostra uma vez

        if (instance == null)
        {
            GameObject go = new GameObject("FimDeJogo");
            instance = go.AddComponent<FimDeJogo>();
        }

        instance.venceu = vitoria;
        instance.ativo = true;

        // congela o jogo
        Time.timeScale = 0f;
        AudioListener.pause = true;

        // desliga os controles do jogador (movimento e câmera) para ele não girar a visão
        CharacterController cc = FindObjectOfType<CharacterController>();
        if (cc != null)
        {
            foreach (MonoBehaviour mb in cc.GetComponentsInChildren<MonoBehaviour>())
                mb.enabled = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void OnGUI()
    {
        if (!ativo) return;

        float w = Screen.width;
        float h = Screen.height;

        // fundo escuro
        Color antes = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.DrawTexture(new Rect(0f, 0f, w, h), Texture2D.whiteTexture);
        GUI.color = antes;

        GUIStyle titulo = new GUIStyle(GUI.skin.label);
        titulo.alignment = TextAnchor.MiddleCenter;
        titulo.fontStyle = FontStyle.Bold;
        titulo.fontSize = Mathf.RoundToInt(h * 0.09f);
        titulo.normal.textColor = venceu ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.3f, 0.3f);

        GUIStyle sub = new GUIStyle(GUI.skin.label);
        sub.alignment = TextAnchor.MiddleCenter;
        sub.fontSize = Mathf.RoundToInt(h * 0.035f);
        sub.normal.textColor = Color.white;

        GUIStyle botao = new GUIStyle(GUI.skin.button);
        botao.fontSize = Mathf.RoundToInt(h * 0.04f);

        GUI.Label(new Rect(0f, h * 0.25f, w, h * 0.16f),
                  venceu ? "VOCÊ CONSEGUIU!" : "VOCÊ FOI PEGO", titulo);

        GUI.Label(new Rect(0f, h * 0.42f, w, h * 0.08f),
                  venceu ? "Todas as provas foram reunidas." : "A criatura te alcançou.", sub);

        if (GUI.Button(new Rect(w * 0.35f, h * 0.58f, w * 0.30f, h * 0.10f), "Jogar de novo", botao))
            Reiniciar();
    }

    void Reiniciar()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}