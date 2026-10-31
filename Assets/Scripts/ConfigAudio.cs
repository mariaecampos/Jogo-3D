using UnityEngine;

// Volumes escolhidos pelo jogador em OPÇÕES (0 a 1), guardados no PlayerPrefs.
// Música: trilhas do menu e do jogo. Efeitos: passos, gritos, ruídos, documentos, golpes.
public static class ConfigAudio
{
    const string ChaveMusica = "DetetiveAbismal_VolumeMusica";
    const string ChaveEfeitos = "DetetiveAbismal_VolumeEfeitos";

    static float musica = -1f;
    static float efeitos = -1f;

    public static float Musica
    {
        get { if (musica < 0f) musica = PlayerPrefs.GetFloat(ChaveMusica, 0.8f); return musica; }
        set { musica = Mathf.Clamp01(value); PlayerPrefs.SetFloat(ChaveMusica, musica); PlayerPrefs.Save(); }
    }

    public static float Efeitos
    {
        get { if (efeitos < 0f) efeitos = PlayerPrefs.GetFloat(ChaveEfeitos, 1f); return efeitos; }
        set { efeitos = Mathf.Clamp01(value); PlayerPrefs.SetFloat(ChaveEfeitos, efeitos); PlayerPrefs.Save(); }
    }
}
