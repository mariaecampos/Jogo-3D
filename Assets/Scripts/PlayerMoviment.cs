using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public float walkSpeed = 3.5f;
    public float runSpeed = 6f;
    public float gravity = -20f;
    // altura do pulo (Espaço), na mesma escala da cena: o jogador da Delegacia tem ~90 de altura
    public float jumpHeight = 30f;

    [Header("Vida")]
    public int vidaMaxima = 3;   // golpes da criatura que o jogador aguenta (o último é fatal)

    CharacterController controller;
    Vector3 velocity;
    Vector3 empurrao;   // golpe da criatura: joga o jogador para trás e vai diminuindo
    bool noChao;        // resultado do movimento vertical (o horizontal não encosta no chão)

    // usados pelos sons de passos
    public bool NoChao { get { return noChao; } }
    public int Vida { get; private set; }
    public bool Andando { get; private set; }
    public bool Correndo { get; private set; }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        Vida = vidaMaxima;
    }

    // golpe da criatura: tira 1 de vida. Retorna true se o jogador ainda está vivo.
    public bool ReceberDano()
    {
        Vida = Mathf.Max(0, Vida - 1);
        return Vida > 0;
    }

    // usado pelo save
    public void DefinirVida(int valor)
    {
        Vida = Mathf.Clamp(valor, 1, vidaMaxima);
    }

    void Update()
    {
        // jogo pausado / em menu: não anda nem pula
        if (Time.timeScale == 0f) return;

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Correndo = Input.GetKey(KeyCode.LeftShift);
        float speed = Correndo ? runSpeed : walkSpeed;
        Vector3 move = (transform.right * x + transform.forward * z);
        if (move.magnitude > 1f) move.Normalize();
        Andando = move.sqrMagnitude > 0.04f;

        controller.Move((move * speed + empurrao) * Time.deltaTime);
        empurrao = Vector3.MoveTowards(empurrao, Vector3.zero, empurrao.magnitude * 3f * Time.deltaTime + 1f);

        // gravidade
        if (noChao && velocity.y < 0f)
            velocity.y = -2f;

        // pulo
        if (noChao && Input.GetKeyDown(KeyCode.Space))
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        velocity.y += gravity * Time.deltaTime;
        CollisionFlags batida = controller.Move(velocity * Time.deltaTime);
        noChao = (batida & CollisionFlags.Below) != 0;

        // bateu a cabeça no teto: começa a cair
        if ((batida & CollisionFlags.Above) != 0 && velocity.y > 0f)
            velocity.y = 0f;
    }

    // joga o jogador na direção indicada (velocidade horizontal + um pequeno pulo)
    public void Empurrar(Vector3 direcao, float forca, float subida)
    {
        direcao.y = 0f;
        empurrao = direcao.normalized * forca;
        velocity.y = subida;
    }
}
