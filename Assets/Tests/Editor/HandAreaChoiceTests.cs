using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class HandAreaChoiceTests
{
    [UnityTest]
    public IEnumerator MageFirstThenChooseInHandAtOpeningAndAfterSevenFullMoves()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/GameScene.unity");
        yield return new EnterPlayMode();
        yield return null;
        var drawer = CardDrawer.Instance;
        Assert.IsNotNull(drawer);
        Assert.AreEqual(0, drawer.CurrentSpellCount);
        Assert.IsNull(Object.FindFirstObjectByType<CardChoicePanel>(), "Spell choices appeared before the mage was selected.");
        var magePicker = Object.FindFirstObjectByType<CharacterChoicePanel>();
        Assert.IsNotNull(magePicker);
        var mage = magePicker.optionsContainer.GetComponentInChildren<CardUI>();
        Assert.IsTrue(mage.cardButton.interactable);
        var selectedMage = mage.cardData;
        mage.cardButton.onClick.Invoke();
        Assert.IsFalse(magePicker.gameObject.activeSelf);
        var king = ChessBoard.Instance.GetPieceAt(new Vector2Int(4, 0));
        Assert.IsNotNull(king);
        for (int round = 1; round <= 2; round++)
        {
            var panel = Object.FindFirstObjectByType<CardChoicePanel>();
            Assert.IsNotNull(panel);
            Assert.IsTrue(CardDrawer.BlocksPlay);
            yield return null; // Allow new UI graphics to enter the canvas raycast list.
            Canvas.ForceUpdateCanvases();
            Assert.IsFalse(magePicker.characterPanelHUD.abilityController.TryActivateMageAbility(selectedMage));
            var hand = (RectTransform)drawer.handPanel;
            var rect = (RectTransform)panel.transform;
            var board = (RectTransform)hand.parent;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Assert.Greater(rect.rect.height, 0);
            Assert.Greater(rect.rect.width, 0);
            foreach (var corner in corners)
            {
                var local = hand.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(hand.rect.xMin - .1f, hand.rect.xMax + .1f));
                Assert.That(local.y, Is.InRange(hand.rect.yMin - .1f, hand.rect.yMax + .1f));
                Assert.LessOrEqual(board.InverseTransformPoint(corner).y, board.rect.yMin + .1f);
            }
            var buttons = panel.GetComponentsInChildren<UnityEngine.UI.Button>();
            Assert.AreEqual(3, buttons.Length);
            var sprites = new HashSet<Sprite>();
            foreach (var button in buttons)
            {
                var art = button.GetComponent<Image>().sprite;
                Assert.IsNotNull(art);
                Assert.IsTrue(sprites.Add(art));
            }
            var chosen = buttons[round];
            var point = RectTransformUtility.WorldToScreenPoint(null, chosen.transform.position);
            var pointer = new PointerEventData(EventSystem.current) { position = point };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits);
            Assert.AreSame(chosen.gameObject, hits[0].gameObject, "Choice must receive the tap above the board.");
            ExecuteEvents.Execute(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Assert.IsFalse(CardDrawer.BlocksPlay);
            Assert.AreEqual(round, drawer.CurrentSpellCount);
            Assert.AreSame(chosen.GetComponent<Image>().sprite,
                drawer.handPanel.GetChild(drawer.handPanel.childCount-1).GetComponent<CardUI>().cardData.fullCardSprite);
            if (round == 1)
            {
                for (int ply = 0; ply < 14; ply++)
                {
                    bool white = ply % 2 == 0;
                    var from = new Vector2Int(ply / 2, white ? 1 : 6);
                    var to = new Vector2Int(from.x, white ? 2 : 5);
                    var pawn = ChessBoard.Instance.GetPieceAt(from);
                    Assert.IsNotNull(pawn);
                    pawn.TryMoveFromTap(to);
                    Assert.AreSame(pawn, ChessBoard.Instance.GetPieceAt(to));
                    Assert.AreEqual(ply == 13, CardDrawer.BlocksPlay);
                }
                yield return null;
            }
        }
        yield return new ExitPlayMode();
    }
}
