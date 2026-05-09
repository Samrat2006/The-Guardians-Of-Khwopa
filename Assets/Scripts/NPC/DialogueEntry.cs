using System;
using UnityEngine;

[Serializable]
public struct DialogueEntry
{
    [TextArea(2, 6)]
    public string text;

    public string speakerName;
    public Sprite portrait;

    public DialogueEntry(string text, string speakerName = null, Sprite portrait = null)
    {
        this.text = text;
        this.speakerName = speakerName;
        this.portrait = portrait;
    }
}

