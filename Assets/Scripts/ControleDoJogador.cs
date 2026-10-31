using UnityEngine;

public class ControleDoJogador : MonoBehaviour
{
    public float velocidadeBase = 120f;
    public float bonusDeVelocidadeShift = 15f;
    public float velocidadeRotacao = 70f;
    public float forcaDoPulo = 10f;
    public float forcaDoPuloDuplo = 12f;
    public int pulosExtrasPermitidos = 1; 
    public Rigidbody oRigidbody;


    private float movimentoVertical;
    private float movimentoHorizontal;
    private bool estaCorrendo;
    private int pulosExtrasRestantes;
    private bool estaNoChao;

    void Start()
    {
        if (oRigidbody == null)
        {
            oRigidbody = GetComponent<Rigidbody>();
        }

        oRigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    void Update()
    {

        movimentoVertical = Input.GetAxisRaw("Vertical") * -1f;
        movimentoHorizontal = Input.GetAxisRaw("Horizontal");
        estaCorrendo = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (estaNoChao)
            {
                ExecutarPulo(forcaDoPulo);
                Debug.Log("PULO 1: Pulo Normal");
            }
            else if (pulosExtrasRestantes > 0)
            {
                ExecutarPulo(forcaDoPuloDuplo);
                pulosExtrasRestantes--;
                Debug.Log("PULO 2: Pulo Duplo!");
            }
        }
    }

    void FixedUpdate()
    {
        Quaternion giro = Quaternion.Euler(0f, movimentoHorizontal * velocidadeRotacao * Time.fixedDeltaTime, 0f);
        oRigidbody.MoveRotation(oRigidbody.rotation * giro);

        float velocidadeAtual = estaCorrendo ? (velocidadeBase + bonusDeVelocidadeShift) : velocidadeBase;
        Vector3 velocidadeAlvo = transform.forward * movimentoVertical * velocidadeAtual;

        velocidadeAlvo.y = oRigidbody.linearVelocity.y;
        oRigidbody.linearVelocity = velocidadeAlvo;
    }

    private void ExecutarPulo(float forca)
    {
        oRigidbody.linearVelocity = new Vector3(oRigidbody.linearVelocity.x, 0f, oRigidbody.linearVelocity.z);

        oRigidbody.AddForce(Vector3.up * forca, ForceMode.Impulse);

        estaNoChao = false;
    }

    private void OnCollisionStay(Collision colisao)
    {
        foreach (ContactPoint contato in colisao.contacts)
        {
            if (contato.normal.y > 0.5f)
            {
                estaNoChao = true;
                pulosExtrasRestantes = pulosExtrasPermitidos; 
                return;
            }
        }
    }

    private void OnCollisionExit(Collision colisao)
    {
        estaNoChao = false;
    }
}