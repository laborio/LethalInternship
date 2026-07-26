// Assigns the local player's transform as the camera follow target.
using Unity.Netcode;
using UnityEngine;

public class LocalPlayerCameraBinder : NetworkBehaviour
{
    private const string DefaultAbilityLoadoutResourcePath = "AbilitySystem/DefaultAbilityLoadout";

    [SerializeField] private CameraController cameraController;
    [SerializeField] private Transform followTarget;
    [SerializeField] private CharacterMovement characterMovement;
    [SerializeField] private Camera abilityCamera;
    [SerializeField] private AbilityLoadoutConfig abilityLoadout;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            return;
        }

        if (cameraController == null)
        {
            cameraController = FindFirstObjectByType<CameraController>();
        }

        if (cameraController != null)
        {
            cameraController.SetTarget(followTarget != null ? followTarget : transform);
            cameraController.SetYawAlignmentTarget(transform);

            if (characterMovement == null)
            {
                characterMovement = GetComponent<CharacterMovement>();
            }

            if (characterMovement != null)
            {
                characterMovement.SetCameraTransform(cameraController.CameraTransform);
            }
        }
        else
        {
            Debug.LogWarning("LocalPlayerCameraBinder: No CameraController found in scene.");
        }

        InitializeAbilities();
    }

    public override void OnNetworkDespawn()
    {
        if (!IsOwner)
        {
            return;
        }

        var abilityController = GetComponent<PlayerAbilityController>();
        if (abilityController != null)
        {
            var uiManager = FindFirstObjectByType<GameplayUiManager>();
            if (uiManager != null)
            {
                uiManager.ClearLocalPlayer(abilityController);
            }
        }
    }

    private void InitializeAbilities()
    {
        if (characterMovement == null)
        {
            characterMovement = GetComponent<CharacterMovement>();
        }

        var abilityController = GetComponent<PlayerAbilityController>();
        if (abilityController == null)
        {
            abilityController = gameObject.AddComponent<PlayerAbilityController>();
        }

        AbilityLoadoutConfig resolvedLoadout = abilityLoadout != null
            ? abilityLoadout
            : Resources.Load<AbilityLoadoutConfig>(DefaultAbilityLoadoutResourcePath);

        Camera resolvedAbilityCamera = abilityCamera != null ? abilityCamera : Camera.main;
        abilityController.Initialize(resolvedLoadout, characterMovement, resolvedAbilityCamera);
        GameplayUiManager.GetOrCreate().BindLocalPlayer(abilityController);
    }
}
