using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace TarodevController
{
    public class HealthBarDisplay : MonoBehaviour
    {
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private Image totalHealthBar;
        [SerializeField] private Image currentHealthBar;

        // Start is called before the first frame update
        void Start()
        {
            totalHealthBar.fillAmount = playerHealth.currentHealth / 10;
            
        }

        // Update is called once per frame
        void Update()
        {
            currentHealthBar.fillAmount = playerHealth.currentHealth / 10; // Current image uses 10 hearts by default so we divide by 10 
        }
    }
}
