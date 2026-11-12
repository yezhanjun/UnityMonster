using MTPSKIT;
using MTPSKIT.Gameplay;
using MTPSKIT.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MTPSKIT.UI.HUD
{

    public class HUDDamagePopupElement : UIElementsLayoutGrid
    {
        [SerializeField] Image _backGround;
        [SerializeField] Text _text;
        [SerializeField] Text _eliminatedText;
        [SerializeField] Text _damageText;
        [SerializeField] UIContentBackground _textBackground;

        [SerializeField] Color _textBackgroundColor;
        [SerializeField] Color _eliminatedTextBackgroundColor;

        Coroutine c_vanish;

        HUDDamagePopup _parent;

        [SerializeField] float _fadeoutAnimationDuration = 0.15f;

        public void Init(HUDDamagePopup parent, float liveTime) { _parent = parent; _liveTime = liveTime; }

        int _damageDealt;

        float _liveTime;


        List<Coroutine> _currentAnimations = new List<Coroutine>();

        public void Set(int damage, Health victim, bool assist = false)
        {
            for (int i = 0; i < _currentAnimations.Count; i++)
            {
                StopCoroutine(_currentAnimations[i]);
            }
            _currentAnimations.Clear();

            gameObject.SetActive(true);

            if (victim != ClientFrontend.ObservedCharacter)
            {
                if (damage >= 0)
                {
                    if (_damageDealt == 0)
                        PlayAnimation(_text.transform, 0.15f, new Vector3(1.3f, 1.3f, 1.3f), Vector3.one);

                    _damageDealt += damage;
                    _text.text = $" 造成伤害";
                    _text.gameObject.SetActive(true);
                    _eliminatedText.gameObject.SetActive(false);
                    _damageText.gameObject.SetActive(true);
                    _damageText.text = $" +{_damageDealt}";

                    _textBackground._rectTransform = _text.rectTransform;
                    _backGround.color = _textBackgroundColor;
                }
                else
                {
                    _backGround.color = _eliminatedTextBackgroundColor;

                    _eliminatedText.text = assist? $" 助攻：{victim.CharacterName} " : $" 击杀：{victim.CharacterName} ";
                    _text.gameObject.SetActive(false);
                    _eliminatedText.gameObject.SetActive(true);
                    _damageText.gameObject.SetActive(false);

                    _textBackground._rectTransform = _eliminatedText.rectTransform;

                    PlayAnimation(_eliminatedText.transform, 0.15f, new Vector3(1.3f, 1.3f, 1.3f), Vector3.one);
                }
            }
            else 
            {
                _eliminatedText.text = $"自我消灭";
                _text.gameObject.SetActive(false);
                _eliminatedText.gameObject.SetActive(true);
                _damageText.gameObject.SetActive(false);
                _textBackground._rectTransform = _eliminatedText.rectTransform;

                _backGround.color = _eliminatedTextBackgroundColor;
            }

            StopVanishCoroutine();
            c_vanish = StartCoroutine(VanishTimer());
            IEnumerator VanishTimer()
            {
                _damageText.transform.localScale = Vector3.one;
                _text.transform.localScale = Vector3.one;
                _eliminatedText.transform.localScale = Vector3.one;
                SetupElements();
                
                _textBackground.OnSizeChanged();
                _parent.GridParent.SetLayoutVertical();
                _parent.GridParent.CalculateLayoutInputVertical();

                PlayAnimation(_damageText.transform, 0.15f, new Vector3(1.3f, 1.3f, 1.3f), Vector3.one);

                yield return new WaitForSeconds(_liveTime - _fadeoutAnimationDuration);
                PlayAnimation(_damageText.transform, _fadeoutAnimationDuration, Vector3.one, new Vector3(0, 0, 1));
                PlayAnimation(_text.transform, _fadeoutAnimationDuration, Vector3.one, new Vector3(0, 0, 1));
                PlayAnimation(_eliminatedText.transform, _fadeoutAnimationDuration, Vector3.one, new Vector3(0, 0, 1));

                yield return new WaitForSeconds(_fadeoutAnimationDuration);

                gameObject.SetActive(false);

                _parent.GridParent.SetLayoutVertical();
                _parent.GridParent.CalculateLayoutInputVertical();

                if (_parent._currentElement == this) _parent._currentElement = null;
            }
        }


        void PlayAnimation(Transform obj, float duration, Vector3 startScale, Vector3 endScale)
        {
            _currentAnimations.Add(StartCoroutine(AnimateObject(obj, duration, startScale, endScale)));
        }

        IEnumerator AnimateObject(Transform obj, float duration, Vector3 startScale, Vector3 endScale)
        {
            yield return new WaitForEndOfFrame();

            float timer = 0;
            while (timer < duration)
            {
                _damageText.transform.localScale = startScale;

                float progress = timer / duration;

                obj.localScale = Vector3.Lerp(startScale, endScale, progress);

                timer += Time.deltaTime;
                yield return null;
            }

            _damageText.transform.localScale = endScale;
        }

        public void ResetPopup()
        {
            _damageDealt = 0;
            StopVanishCoroutine();
        }

        private void OnDisable()
        {
            StopVanishCoroutine();
        }

        void StopVanishCoroutine()
        {
            if (c_vanish != null)
            {
                StopCoroutine(c_vanish);
                c_vanish = null;
            }
        }
    }
}