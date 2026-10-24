using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace GateRush.UI
{
    /// <summary>
    /// Flies gold to its counter (Module 47): a fixed set of coin images, made
    /// once, on their own canvas, moved along the arcs <see cref="CoinFlight"/>
    /// lays out from a source to a counter's pill; each landing adds its share
    /// to the counter (<see cref="GoldCounter"/>) and pops it.
    /// </summary>
    /// <remarks>
    /// <para><b>Its own canvas.</b> The coins sit on a nested canvas that sorts
    /// above every scrim (<see cref="UiConfig.CoinFlightSortingOffset"/>), so
    /// moving them rebuilds that small canvas only. It has no raycaster, and
    /// the coins are no raycast targets: they take no input. While coins fly,
    /// the counter's pill — a nested canvas of its own — is lifted to sort
    /// over any scrim too (<see cref="UiConfig.GoldLiftSortingOffset"/>), and
    /// set back when the flight ends.</para>
    /// <para><b>No allocation in flight.</b> The coins are made in
    /// <c>Awake</c>, <see cref="UiConfig.CoinFlightCap"/> of them, and reused;
    /// one tween on unscaled time, carrying this view as its id, moves them
    /// all from arrays filled when the flight starts.</para>
    /// <para><b>Ending.</b> After the last landing, and at once on
    /// <see cref="Stop"/>, on disable and on destroy, the coins hide, the
    /// counter is released to the wallet's value and the pill is set back.</para>
    /// </remarks>
    public sealed class CoinFlightView : MonoBehaviour
    {
        private const string CoinName = "Coin";

        [Tooltip("Assets/Config/UiConfig.asset: the coins' count, size, timing and sorting.")]
        [SerializeField] private UiConfig config;

        [Tooltip("The interface's coin (Module 45).")]
        [SerializeField] private Sprite coinSprite;

        [Tooltip("The nested canvas on this object the coins fly in.")]
        [SerializeField] private Canvas layer;

        private Image[] coins;
        private RectTransform[] coinRects;
        private Vector2[] starts;
        private float[] delays;
        private float[] durations;
        private float[] arcs;
        private int[] shares;
        private bool[] isLanded;
        private int coinCount;
        private Vector2 end;
        private float startDelay;
        private float lastTapTime = float.NegativeInfinity;
        private GoldCounter counter;
        private Canvas lifted;
        private TweenCallback<float> tick;
        private TweenCallback finish;

        /// <summary>Raised when a coin lands, at most once per <see cref="UiConfig.CoinTapMinIntervalSeconds"/>: the moment for UiTap.</summary>
        public event Action CoinLanded;

        /// <summary>True from <see cref="Fly"/> until the last coin lands or the flight is stopped.</summary>
        public bool IsFlying { get; private set; }

        /// <summary>Adds each unassigned part to <paramref name="missing"/>.</summary>
        public void AddMissing(List<string> missing)
        {
            UiParts.AddIfMissing(missing, config, nameof(CoinFlightView), nameof(config));
            UiParts.AddIfMissing(missing, coinSprite, nameof(CoinFlightView), nameof(coinSprite));
            UiParts.AddIfMissing(missing, layer, nameof(CoinFlightView), nameof(layer));
        }

        /// <summary>
        /// Flies <paramref name="pieces"/> from <paramref name="from"/>'s centre
        /// to <paramref name="to"/>'s, after <paramref name="delaySeconds"/>:
        /// holds <paramref name="target"/> at <paramref name="shownGold"/>, adds
        /// each coin's share to it as it lands, and lifts
        /// <paramref name="targetCanvas"/>, the pill's own canvas, over any scrim
        /// meanwhile. A flight still running is stopped first, which releases
        /// its counter, so the hold is taken after it. With no coins, or no
        /// source or target, the counter is released at once.
        /// </summary>
        public void Fly(
            RectTransform from, RectTransform to, IReadOnlyList<CoinPiece> pieces, float delaySeconds, GoldCounter target,
            int shownGold, Canvas targetCanvas)
        {
            Stop();
            if (target == null)
            {
                return;
            }

            counter = target;
            target.Hold(shownGold);
            if (from == null || to == null || pieces == null || pieces.Count == 0 || coins == null)
            {
                Finish();
                return;
            }

            var area = (RectTransform)layer.transform;
            var source = LocalCentre(area, from);
            end = LocalCentre(area, to);
            coinCount = Math.Min(pieces.Count, coins.Length);
            startDelay = delaySeconds;
            var total = 0f;
            for (var i = 0; i < coinCount; i++)
            {
                var piece = pieces[i];
                starts[i] = source + piece.StartOffset;
                delays[i] = piece.DelaySeconds;
                durations[i] = piece.Seconds;
                arcs[i] = piece.ArcUnits;
                shares[i] = piece.Share;
                isLanded[i] = false;
                total = Mathf.Max(total, piece.DelaySeconds + piece.Seconds);
            }

            // A capped pool keeps the gold all the same: what the coins left
            // out would carry rides on the last one.
            for (var i = coinCount; i < pieces.Count; i++)
            {
                shares[coinCount - 1] += pieces[i].Share;
            }

            Lift(targetCanvas);
            IsFlying = true;
            DOVirtual.Float(0f, startDelay + total, startDelay + total, tick)
                .SetEase(Ease.Linear)
                .SetUpdate(true)
                .SetId(this)
                .OnComplete(finish);
        }

        /// <summary>Ends a flight at once, if one is running: the coins hide, the counter shows the wallet's value, and the pill sorts as before.</summary>
        public void Stop()
        {
            DOTween.Kill(this);
            if (IsFlying || counter != null)
            {
                Finish();
            }
        }

        private void Awake()
        {
            tick = Tick;
            finish = Finish;
            if (config == null || layer == null)
            {
                return;
            }

            var size = config.CoinFlightCap;
            coins = new Image[size];
            coinRects = new RectTransform[size];
            starts = new Vector2[size];
            delays = new float[size];
            durations = new float[size];
            arcs = new float[size];
            shares = new int[size];
            isLanded = new bool[size];
            var side = new Vector2(config.CoinFlightSizeUnits, config.CoinFlightSizeUnits);
            for (var i = 0; i < size; i++)
            {
                var coin = new GameObject(CoinName, typeof(RectTransform));
                var rect = (RectTransform)coin.transform;
                rect.SetParent(layer.transform, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = side;
                var image = coin.AddComponent<Image>();
                image.sprite = coinSprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
                image.enabled = false;
                coins[i] = image;
                coinRects[i] = rect;
            }
        }

        /// <summary>
        /// Sorts the coins' canvas above the screen's canvas by
        /// <see cref="UiConfig.CoinFlightSortingOffset"/>, each time the view is
        /// enabled: only then is it under the screen's canvas, where a nested
        /// canvas's override takes effect. The prefab's own value is not relied
        /// on — a canvas built with none above it cannot keep it set.
        /// </summary>
        private void OnEnable()
        {
            if (config == null || layer == null || layer.isRootCanvas)
            {
                return;
            }

            layer.overrideSorting = true;
            layer.sortingOrder = layer.rootCanvas.sortingOrder + config.CoinFlightSortingOffset;
        }

        private void OnDisable()
        {
            Stop();
        }

        private void OnDestroy()
        {
            DOTween.Kill(this);
        }

        private void Tick(float time)
        {
            var endScale = config.CoinFlightEndScale;
            var ease = config.CoinFlightEase;
            for (var i = 0; i < coinCount; i++)
            {
                if (isLanded[i])
                {
                    continue;
                }

                var u = (time - startDelay - delays[i]) / durations[i];
                if (u < 0f)
                {
                    continue;
                }

                if (u >= 1f)
                {
                    Land(i);
                    continue;
                }

                var image = coins[i];
                if (!image.enabled)
                {
                    image.enabled = true;
                }

                var eased = DOVirtual.EasedValue(0f, 1f, u, ease);
                var position = Vector2.LerpUnclamped(starts[i], end, eased);
                position.y += arcs[i] * 4f * eased * (1f - eased);
                var rect = coinRects[i];
                rect.anchoredPosition = position;
                var scale = Mathf.LerpUnclamped(1f, endScale, eased);
                rect.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private void Land(int index)
        {
            isLanded[index] = true;
            coins[index].enabled = false;
            counter?.Add(shares[index]);

            var now = Time.unscaledTime;
            if (now - lastTapTime >= config.CoinTapMinIntervalSeconds)
            {
                lastTapTime = now;
                CoinLanded?.Invoke();
            }
        }

        private void Finish()
        {
            IsFlying = false;
            if (coins != null)
            {
                for (var i = 0; i < coins.Length; i++)
                {
                    if (coins[i] != null)
                    {
                        coins[i].enabled = false;
                    }
                }
            }

            coinCount = 0;
            Lower();
            var released = counter;
            counter = null;
            released?.Release();
        }

        /// <summary>Sorts the pill's canvas over any scrim, under the coins.</summary>
        private void Lift(Canvas pill)
        {
            if (pill == null || pill.isRootCanvas)
            {
                return;
            }

            lifted = pill;
            pill.overrideSorting = true;
            pill.sortingOrder = pill.rootCanvas.sortingOrder + config.GoldLiftSortingOffset;
        }

        /// <summary>Sets the lifted pill back to sorting with its parent.</summary>
        private void Lower()
        {
            if (lifted != null)
            {
                lifted.overrideSorting = false;
            }

            lifted = null;
        }

        /// <summary>The centre of <paramref name="rect"/>, in <paramref name="area"/>'s local frame: the same screen, any parent.</summary>
        private static Vector2 LocalCentre(RectTransform area, RectTransform rect) =>
            area.InverseTransformPoint(rect.TransformPoint(rect.rect.center));
    }
}
