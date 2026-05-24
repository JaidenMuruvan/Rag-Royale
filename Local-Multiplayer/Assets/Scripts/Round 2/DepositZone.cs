using System;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class DepositZone : MonoBehaviour
{
    public event Action<int> OnPlayerEnter;
    public event Action<int> OnPlayerExit;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        var ctrl = other.GetComponentInParent<MultiplayerPlayerController>();
        if (ctrl != null)
            OnPlayerEnter?.Invoke(ctrl.PlayerID);
    }

    private void OnTriggerExit(Collider other)
    {
        var ctrl = other.GetComponentInParent<MultiplayerPlayerController>();
        if (ctrl != null)
            OnPlayerExit?.Invoke(ctrl.PlayerID);
    }
}
