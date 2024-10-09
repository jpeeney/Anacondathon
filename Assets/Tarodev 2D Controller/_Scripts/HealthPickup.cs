using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TarodevController
{
    public class HealthPickup : MonoBehaviour
    {
        [SerializeField] private float healthValue;
        private bool _beingPulled;
        private GameObject _snake;
        [SerializeField] public GameObject _player;

        void Update()
        {
            if (_beingPulled) transform.position = _snake.activeSelf ? _snake.transform.position : _player.transform.position;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.tag == "Player")
            {
                collision.GetComponent<PlayerHealth>().AddHealth(healthValue);
                gameObject.SetActive(false);
            }
            else if (collision.tag == "Snake")
            {
                _beingPulled = true;
                _snake = collision.gameObject;
            }
            // if it's the snake, snap to the snake's position + an offset (depending on grapple direction when hit) until it's done retracting or this object touches the player
        }
    }
}