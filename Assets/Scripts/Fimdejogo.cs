using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tela de vitória / game over. Não precisa de nada na cena: ela se cria sozinha.
// Uso: FimDeJogo.Vitoria();  ou  FimDeJogo.Derrota();
public class FimDeJogo : TelaAbismal
{
    static FimDeJogo instance;

    // true enquanto a tela de vitória / game over estiver aberta
    public static bool Ativo { get { return instance != null; } }

    public static void Vitoria() { Mostrar(true); }
    public static void Derrota() { Mostrar(false); }

    static void Mostrar(bool vitoria)
    {
        if (instance != null) return;   // só mostra uma vez

        // fecha a pausa, se estiver aberta, e o caso acabou: não tem mais o que continuar
        MenuPausa.Fechar();
        SaveJogo.Apagar();

        // congela o jogo
        Time.timeScale = 0f;
        AudioListener.pause = true;

        // desliga os controles do jogador (movimento e câmera) para ele não girar a visão
        CharacterController cc = FindAnyObjectByType<CharacterController>();
        if (cc != null)
        {
            foreach (MonoBehaviour mb in cc.GetComponentsInChildren<MonoBehaviour>())
                mb.enabled = false;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        instance = new GameObject("FimDeJogo").AddComponent<FimDeJogo>();
        instance.Montar(vitoria);
    }

    void Montar(bool vitoria)
    {
        string cena = SceneManager.GetActiveScene().name;

        Config cfg;
        if (vitoria)
        {
            cfg = new Config
            {
                tituloPequeno = "CASO",
                tituloGrande = "RESOLVIDO",
                corGrande = corOsso,
                corBrilho = new Color(1f, 0.9f, 0.7f, 0.35f),
                piscar = false,
                lema = "Todas as provas foram reunidas. A verdade veio à tona.",
                fichaCampos = new[]
                {
                    "CASO", "0713",
                    "LOCAL", cena,
                    "PROVAS COLETADAS", ProvasColetadas(),
                    "STATUS", "<color=#DCD1BC>RESOLVIDO</color>",
                },
                carimbo = "CASO ENCERRADO",
                corCarimbo = new Color(0.86f, 0.82f, 0.74f, 0.6f),
            };
        }
        else
        {
            cfg = new Config
            {
                tituloPequeno = "VOCÊ FOI",
                tituloGrande = "PEGO",
                lema = "A criatura te alcançou. Mais um nome para a lista.",
                fichaCampos = new[]
                {
                    "CASO", "0713",
                    "LOCAL", cena,
                    "PROVAS COLETADAS", ProvasColetadas(),
                    "STATUS", "<color=#B01818>JORNALISTA DESAPARECIDO</color>",
                },
                carimbo = "ARQUIVO MORTO",
            };
        }

        Montar(cfg,
            ("JOGAR DE NOVO", () => TrocarDeCena(cena, false)),
            ("MENU PRINCIPAL", () => TrocarDeCena(MenuPrincipal.CenaInicial ?? cena, true)),
            ("SAIR", SairDoJogo));

        TocarMusica("abyss_sound", 0.6f);
        StartCoroutine(Aparecer());
    }

    IEnumerator Aparecer()
    {
        // tela escurece aos poucos, como um desmaio
        grupoRaiz.alpha = 0f;
        yield return Fade(grupoRaiz, 0f, 1f, 1.2f);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
