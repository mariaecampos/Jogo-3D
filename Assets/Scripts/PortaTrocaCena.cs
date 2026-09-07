using UnityEngine;
using UnityEngine.SceneManagement;

public class PortaTrocaCena : MonoBehaviour
{
    public string nomeDaCenaDestino;


    private void OnTriggerEnter(Collider outro)
    {
        if (outro.gameObject.CompareTag("Player"))
        {
            SceneManager.LoadScene(nomeDaCenaDestino);
        }
    }
}