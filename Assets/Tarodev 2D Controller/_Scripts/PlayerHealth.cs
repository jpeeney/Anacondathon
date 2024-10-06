using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TarodevController
{
    public class PlayerHealth : MonoBehaviour
    {
        private PlayerController controller;
        private Rigidbody2D _rb;

        [Header ("Health")]
        [SerializeField] private float startingHealth;
        public float currentHealth {get; private set;}
        private bool dead;

        [Header ("iFrames")]
        [SerializeField] private float iFramesDuration;
        [SerializeField] private int numFlashes;
        private SpriteRenderer spriteRend;

        // Start is called before the first frame update
        void Awake()
        {
            currentHealth = startingHealth;
            controller = GetComponent<PlayerController>();
            spriteRend = GetComponentInChildren<SpriteRenderer>();
            _rb = GetComponent<Rigidbody2D>();
        }

        public void TakeDamage(float _damage, Vector2 _knockback)
        {
            currentHealth = Mathf.Clamp(currentHealth - _damage, 0, startingHealth); // Doesn't let the health go below 0 or above starting health

            if (currentHealth > 0)
            {
                if (_knockback != Vector2.zero) controller.HandleKnockback(_knockback);
                StartCoroutine(Invulnerability());
            }
            else
            {
                if (!dead)
                {
                    controller.enabled = false;
                    _rb.velocity = Vector2.zero;
                    dead = true;

                }
                //player dead
            }
        }

        public void AddHealth(float _value)
        {
            currentHealth = Mathf.Clamp(currentHealth + _value, 0, startingHealth);
        }

        private IEnumerator Invulnerability()
        {
            Physics2D.IgnoreLayerCollision(6, 7, true); //Player layer, enemy layer
            //cycle through frames
            for (int i = 0; i < numFlashes; i++)
            {
                spriteRend.color = new Color(spriteRend.color.r, spriteRend.color.g, spriteRend.color.b, 0.5f);
                yield return new WaitForSeconds(iFramesDuration / (numFlashes * 2));
                spriteRend.color = new Color(spriteRend.color.r, spriteRend.color.g, spriteRend.color.b, 255);
                yield return new WaitForSeconds(iFramesDuration / (numFlashes * 2));
            }
            Physics2D.IgnoreLayerCollision(6, 7, false);

        }
    }
}
