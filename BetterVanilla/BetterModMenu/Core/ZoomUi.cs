using BetterVanilla.Components;
using BetterVanilla.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BetterVanilla.BetterModMenu.Core;

public sealed class ZoomUi : MonoBehaviour
{
    public Button zoomInButton = null!;
    public Button zoomOutButton = null!;

    public void OnZoomInButtonClicked()
    {
        BetterVanillaManager.Instance.ZoomBehaviour.ZoomIn(3f);
    }

    public void OnZoomOutButtonClicked()
    {
        BetterVanillaManager.Instance.ZoomBehaviour.ZoomOut(3f);
    }

    private void Update()
    {
        if (zoomInButton != null)
        {
            zoomInButton.interactable = BetterVanillaManager.Instance.ZoomBehaviour.CanZoomIn(3f);
        }

        if (zoomOutButton != null)
        {
            zoomOutButton.interactable = BetterVanillaManager.Instance.ZoomBehaviour.CanZoomOut(3f);
        }
    }
}