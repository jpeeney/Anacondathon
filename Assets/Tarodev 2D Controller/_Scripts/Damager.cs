using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TarodevController
{
    public class Damager : MonoBehaviour
    {
        [SerializeField] private float damage;
        [SerializeField] private Vector2 knockback;

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.tag == "Player")
            {
                collision.GetComponent<PlayerHealth>().TakeDamage(damage, knockback);
            }
        }

        public float getDamage()
        {
            return damage;
        }
    }
}