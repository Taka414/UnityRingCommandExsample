using System.Collections.Generic;
using DG.Tweening;
using Takap.Utility;
using UnityEngine;

namespace Takap.Samples
{
    /// <summary>
    /// リングメニューを制御するためのクラスです。
    /// </summary>
    public class RingCmdControl : MonoBehaviour
    {
        //
        // Inspectors
        // - - - - - - - - - - - - - - - - - - - -

        // リング状に配置するオブジェクト
        [SerializeField] private List<RingItem> itemList = new List<RingItem>();
        // リングの横幅
        [SerializeField] private float ringWidth;
        // リングの縦幅
        [SerializeField] private float ringHeight;
        // 移動のインターバル
        [SerializeField] private float magnetSpeed = 0.18f;
        // 要素が一番後ろに移動したときの縮小率(0.5 = 半分の大きさ)
        [SerializeField] private float backZoomScale = 0.5f;

        //
        // Fields
        // - - - - - - - - - - - - - - - - - - - -

        // 初期化を呼び出したかどうかのフラグ
        // true : 呼び出した / false : まだ
        private bool isInit;
        // 左右の回転量
        private float stepAmount;
        // 要素の間隔・角度
        private float oneAngle;
        // 目標位置 -> 回転させた回数(右+1, 左-1)
        private int count;
        // リングの前後関係整列用のバッファー
        private List<RingItem> itemListCache = new List<RingItem>();
        // アニメーション保持用の変数
        private Tween anim;

        //
        // Runtime impl
        // - - - - - - - - - - - - - - - - - - - -

        private void Awake() => this.enabled = false;

        private void OnDestroy() => this.anim.Kill();

        private void Update()
        {
            if (!this.isInit)
            {
                return;
            }
            this.updateItemsPostion();
        }

        private void OnValidate()
        {
            if (!this.isInit)
            {
                return;
            }
            this.updateItemsPostion();
        }

        //
        // Public Methods
        // - - - - - - - - - - - - - - - - - - - -

        /// <summary>
        /// リングメニュー要素を1つ追加します。
        /// </summary>
        /// <remarks>
        /// 基本的にインスペクターから設定済み or 使う前にこのメソッドで要素を追加してから
        /// Init() を呼んで利用を開始することを想定する。簡単のために Init 後に動的に要素を増減は想定しない。
        /// </remarks>
        public void AddRingItem(RingItem item)
        {
            if (this.isInit)
            {
                Debug.LogWarning("初期化した後は追加できません。");
                return;
            }
            this.itemList.Add(item);
        }

        /// <summary>
        /// リングを初期化して使用できるようにします。
        /// </summary>
        public void Init()
        {
            if (this.isInit)
            {
                Debug.LogWarning("既に初期化済みです。");
                return;
            }

            // 持ってる要素数に応じて初期位置を計算する
            this.oneAngle = 360.0f / this.itemList.Count;
            for (int i = 0; i < this.itemList.Count; i++)
            {
                RingItem item = this.itemList[i];

                // リストの先頭の要素が一番前に来るように調整
                item.InitDegree = (this.oneAngle * i) + 270.0f;
            }

            // 並び順用の整列用のキャッシュを作成
            this.itemListCache.AddRange(this.itemList);

            // 開始時にアニメーション付けたいときはここらにDOTweenで何か記述する
            // ...

            this.isInit = true;

            this.updateItemsPostion(); // 位置と大きさを決めるために1回だけ呼び出す
        }

        /// <summary>
        /// リングを右に1つ回転します。
        /// </summary>
        public void TurnRight()
        {
            if (!this.isInit)
            {
                Debug.LogWarning("初期化されていません");
                return;
            }

            this.count++;
            float endValue = this.count * this.oneAngle;

            this.anim.Kill();
            this.enabled = true;

            // GCAlloc -> 1.2K
            var seq = DOTween.Sequence();
            seq.Append(DOTween.To(() => this.stepAmount, val => this.stepAmount = val, endValue, this.magnetSpeed));
            seq.AppendCallback(() => this.onTurnCompleted());
            this.anim = seq;
        }

        /// <summary>
        /// リングを左に1つ回転します。
        /// </summary>
        public void TurnLeft()
        {
            if (!this.isInit)
            {
                Debug.LogWarning("初期化されていません");
                return;
            }

            this.count--;
            float endValue = this.count * this.oneAngle;

            this.anim.Kill();
            this.enabled = true;

            // GCAlloc -> 1.2K
            var seq = DOTween.Sequence();
            seq.Append(DOTween.To(() => this.stepAmount, val => this.stepAmount = val, endValue, this.magnetSpeed));
            seq.AppendCallback(() => this.onTurnCompleted());
            this.anim = seq;
        }

        //
        // Non-Public Methods
        // - - - - - - - - - - - - - - - - - - - -

        // リング要素の位置をすべて更新する
        private void updateItemsPostion()
        {
            foreach (RingItem item in this.itemList)
            {
                if (item == null)
                {
                    Debug.LogWarning("要素がnullです。");
                    continue;
                }

                float deg = (item.InitDegree + this.stepAmount) % 360.0f;
                float _z = Mathf.Abs(deg - 270.0f);
                if (_z > 180.0f)
                {
                    _z = Mathf.Abs(360.0f - _z); // 180が一番うしろ
                }
                item.Rect.SetAnchoredPosZ(_z);

                // 一番後ろが指定した大きさになるように大きさを変更
                item.Rect.SetLocalScaleXY(Mathf.Lerp(this.backZoomScale, 1.0f, 1.0f - Mathf.InverseLerp(0, 180.0f, _z)));

                var (x, y) = MathfUtil.GetPosDeg(deg);
                item.Rect.SetAnchoredPos(x * this.ringWidth, y * this.ringHeight);
            }

            // 計算したZ位置からuGUIの前後関係を設定する
            this.itemListCache.Sort(this.sort);
            for (int i = 0; i < this.itemListCache.Count; i++)
            {
                this.itemListCache[i].Rect.SetSiblingIndex(i);
            }
        }

        // 回転が終了したときに呼び出されるコールバック
        private void onTurnCompleted()
        {
            this.enabled = false;
        }

        // 要素を整列するときに渡すラムダ用の処理
        private int sort(RingItem a, RingItem b)
        {
            float diff = b.Rect.GetAnchoredPosZ() - a.Rect.GetAnchoredPosZ();
            if (diff > 0)
            {
                return 1;
            }
            else if (diff < 0)
            {
                return -1;
            }
            return 0;
        }
    }
}