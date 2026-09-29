using System;
using UnityEngine;

namespace KitchenChaos.Players
{
    /// <summary>
    /// Interface used on objects able to drive a <see cref="Player"/>.
    /// </summary>
    public interface IPlayerInputSource
    {
        event Action<Vector2> OnMove;
        event Action OnCollectItem;
        event Action OnInteractWithEnvironment;
    }
}
