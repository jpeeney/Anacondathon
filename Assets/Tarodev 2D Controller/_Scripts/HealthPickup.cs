using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TarodevController
{
    public class HealthPickup : MonoBehaviour
    {
        [SerializeField] private float healthValue;

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.tag == "Player")
            {
                collision.GetComponent<PlayerHealth>().AddHealth(healthValue);
                gameObject.SetActive(false);
            }
        }
    }
}