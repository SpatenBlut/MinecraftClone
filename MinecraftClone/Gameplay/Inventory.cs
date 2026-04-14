using System;
using Microsoft.Xna.Framework.Input;
using MinecraftClone.World;

namespace MinecraftClone.Gameplay;

public class Inventory
{
    private BlockType[] _hotbar;
    private int _selectedSlot;
    private KeyboardState _lastKeyState;

    public BlockType SelectedBlock => _hotbar[_selectedSlot];
    public int SelectedSlot => _selectedSlot;
    public BlockType[] Hotbar => _hotbar;

    public Inventory()
    {
        _hotbar = new BlockType[9];
        _hotbar[0] = BlockType.Grass;
        _hotbar[1] = BlockType.Dirt;
        _hotbar[2] = BlockType.Stone;
        _hotbar[3] = BlockType.Wood;
        _hotbar[4] = BlockType.Leaves;
        _hotbar[5] = BlockType.Sand;
        _hotbar[6] = BlockType.Air;
        _hotbar[7] = BlockType.Air;
        _hotbar[8] = BlockType.Air;

        _selectedSlot = 0;
    }

    public void Update()
    {
        var keyState = Keyboard.GetState();

        // Hotbar-Auswahl mit 1-9
        for (int i = 0; i < 9; i++)
        {
            Keys key = Keys.D1 + i;
            if (keyState.IsKeyDown(key) && _lastKeyState.IsKeyUp(key))
            {
                _selectedSlot = i;
            }
        }

        // Mausrad (optional, noch nicht implementiert)

        _lastKeyState = keyState;
    }

    public void SetSlot(int slot, BlockType blockType)
    {
        if (slot >= 0 && slot < 9)
            _hotbar[slot] = blockType;
    }

    public void AddBlock(BlockType blockType)
    {
        // Suche freien Slot oder gleichen Block-Typ
        for (int i = 0; i < 9; i++)
        {
            if (_hotbar[i] == BlockType.Air)
            {
                _hotbar[i] = blockType;
                return;
            }
        }
    }
}
