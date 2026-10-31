using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Save do jogo (um slot só, guardado no PlayerPrefs).
// Salva quando o jogador sai pelo menu de pausa e é apagado no fim de jogo ou em "Nova investigação".
public static class SaveJogo
{
    const string Chave = "DetetiveAbismal_Save_v3";   // v3: documentos guardam a posição (podem ter sido roubados e mudado de lugar)

    [Serializable]
    public class Dados
    {
        public string cena;
        public Vector3 posicao;
        public float giroY;
        public int vida;
        public int documentosColetados;
        public int totalDocumentos;
        public List<EstadoDocumento> documentos = new List<EstadoDocumento>();
        public bool temLanterna;
        public bool lanternaLigada;
        public float bateria;
        public string data;
    }

    [Serializable]
    public class EstadoDocumento
    {
        public string nome;
        public bool coletado;
        public Vector3 posicao;
    }

    static bool aplicarNaProximaCena;

    public static bool Existe { get { return PlayerPrefs.HasKey(Chave); } }

    public static Dados Ler()
    {
        if (!Existe) return null;
        try { return JsonUtility.FromJson<Dados>(PlayerPrefs.GetString(Chave)); }
        catch { return null; }
    }

    public static void Apagar()
    {
        PlayerPrefs.DeleteKey(Chave);
        PlayerPrefs.Save();
    }

    public static void Salvar()
    {
        PlayerMovement jogador = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
        if (jogador == null || FimDeJogo.Ativo) return;

        Dados d = new Dados();
        d.cena = SceneManager.GetActiveScene().name;
        d.posicao = jogador.transform.position;
        d.giroY = jogador.transform.eulerAngles.y;
        d.vida = jogador.Vida;
        d.data = DateTime.Now.ToString("dd/MM HH:mm");

        GerenciadorDocumento g = GerenciadorDocumento.Instance;
        if (g != null)
        {
            d.documentosColetados = g.DocumentosColetados;
            d.totalDocumentos = g.totalDocumentos;
        }
        // inclui os desligados (já pegos)
        foreach (ColetorDocumento doc in UnityEngine.Object.FindObjectsByType<ColetorDocumento>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!doc.gameObject.scene.IsValid()) continue;
            d.documentos.Add(new EstadoDocumento { nome = doc.name, coletado = doc.Coletado, posicao = doc.transform.position });
        }

        Flashlight lanterna = UnityEngine.Object.FindAnyObjectByType<Flashlight>();
        if (lanterna != null)
        {
            d.temLanterna = true;
            d.lanternaLigada = lanterna.Ligada;
            d.bateria = lanterna.Bateria;
        }

        PlayerPrefs.SetString(Chave, JsonUtility.ToJson(d));
        PlayerPrefs.Save();
    }

    // "Continuar": se o save é de outra cena, carrega ela antes de aplicar
    public static void Continuar()
    {
        Dados d = Ler();
        if (d == null) return;

        if (d.cena == SceneManager.GetActiveScene().name)
        {
            Aplicar(d);
            return;
        }

        if (!aplicarNaProximaCena)
            SceneManager.sceneLoaded += AoCarregarCena;
        aplicarNaProximaCena = true;
        SceneManager.LoadScene(d.cena);
    }

    static void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        aplicarNaProximaCena = false;
        Dados d = Ler();
        if (d != null) Aplicar(d);
    }

    static void Aplicar(Dados d)
    {
        // jogador volta para onde estava
        PlayerMovement jogador = UnityEngine.Object.FindAnyObjectByType<PlayerMovement>();
        if (jogador != null)
        {
            CharacterController cc = jogador.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            jogador.transform.position = d.posicao;
            jogador.transform.rotation = Quaternion.Euler(0f, d.giroY, 0f);
            if (d.vida > 0) jogador.DefinirVida(d.vida);
            if (cc != null) cc.enabled = true;
        }

        // cada documento volta para onde estava; os já pegos voltam para o "bolso" do jogador
        Dictionary<string, EstadoDocumento> porNome = new Dictionary<string, EstadoDocumento>();
        foreach (EstadoDocumento e in d.documentos) porNome[e.nome] = e;

        foreach (ColetorDocumento doc in UnityEngine.Object.FindObjectsByType<ColetorDocumento>(FindObjectsSortMode.None))
        {
            if (!porNome.TryGetValue(doc.name, out EstadoDocumento e)) continue;

            doc.transform.position = e.posicao;
            if (e.coletado)
            {
                doc.MarcarColetado();
                if (GerenciadorDocumento.Instance != null)
                    GerenciadorDocumento.Instance.AdicionarDocumento(doc);
            }
        }

        Flashlight lanterna = UnityEngine.Object.FindAnyObjectByType<Flashlight>();
        if (lanterna != null && d.temLanterna)
        {
            lanterna.Bateria = d.bateria;
            lanterna.Definir(d.lanternaLigada);
        }
    }
}
