using UnityEngine;

public class ItemRotator : MonoBehaviour
{
    public float speedX = 0f;
    public float speedY = 50f;
    public float speedZ = 0f; 

    void Update()
    {
        transform.Rotate(new Vector3(speedX, speedY, speedZ) * Time.deltaTime);
    }
}