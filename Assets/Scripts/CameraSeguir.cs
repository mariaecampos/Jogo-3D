using UnityEngine;

public class CameraSeguir : MonoBehaviour
{
    public Transform target;//jogador
    public Vector3 offset = new Vector3(0f, .8f, 3);//distancia da camera
    public float smoothSpeed = 8f;//velocidade da transição
    public bool girarComOPlayer = true;//gira junto com as costas do player
    public float alturaDoFoco = 6f; //altura para onde a câmera aponta

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition;

        if (girarComOPlayer)
        {
            desiredPosition = target.TransformPoint(offset);
        }
        else
        {
            desiredPosition = target.position + offset;
        }

        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.position = smoothedPosition;

        //aponta a visão da câmera para o jogador
        transform.LookAt(target.position + Vector3.up * alturaDoFoco);
    }
}