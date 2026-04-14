using System;
using Microsoft.Xna.Framework.Input;
using MinecraftClone.World;

namespace MinecraftClone.Gameplay;

// ── Item-Stack ────────────────────────────────────────────────────────────────

public struct ItemStack
{
    public BlockType Block;
    public int Count;

    public bool IsEmpty => Block == BlockType.Air || Count <= 0;
    public static ItemStack Empty => new() { Block = BlockType.Air, Count = 0 };

    public ItemStack(BlockType block, int count) { Block = block; Count = count; }
}

// ── Inventar ──────────────────────────────────────────────────────────────────

public class Inventory
{
    // Slot-Indizes:
    //  0 –  8  Hotbar         (9 Slots)
    //  9 – 35  Hauptinventar  (27 Slots)
    // 36 – 39  Rüstung        (dekorativ)
    // 40 – 43  Fertigung 2×2  (dekorativ)
    //     44   Fertigungsausgabe (dekorativ)

    public const int HotbarStart   = 0;
    public const int HotbarEnd     = 9;
    public const int MainStart     = 9;
    public const int MainEnd       = 36;
    public const int TotalSlots    = 45;
    public const int MaxStack      = 64;

    private readonly ItemStack[] _slots = new ItemStack[TotalSlots];
    private ItemStack _cursorStack;

    private int _selectedSlot;
    private int _lastScrollValue;
    private KeyboardState _lastKeyState;

    public int SelectedSlot => _selectedSlot;
    public BlockType SelectedBlock => _slots[_selectedSlot].IsEmpty
        ? BlockType.Air
        : _slots[_selectedSlot].Block;
    public ItemStack CursorStack => _cursorStack;

    public ItemStack GetSlot(int idx) => idx >= 0 && idx < TotalSlots
        ? _slots[idx]
        : ItemStack.Empty;

    public static bool IsInteractiveSlot(int idx) => idx >= 0 && idx < MainEnd;

    public Inventory()
    {
        // Start-Ausrüstung im Hotbar (Stacks à 64)
        _slots[0] = new ItemStack(BlockType.Grass,  64);
        _slots[1] = new ItemStack(BlockType.Dirt,   64);
        _slots[2] = new ItemStack(BlockType.Stone,  64);
        _slots[3] = new ItemStack(BlockType.Wood,   64);
        _slots[4] = new ItemStack(BlockType.Leaves, 64);
        _slots[5] = new ItemStack(BlockType.Sand,   64);
    }

    // ── Aufruf pro Frame (nicht pro Tick) ────────────────────────────────────

    public void Update(KeyboardState keyState, int scrollWheelValue)
    {
        // Hotbar-Auswahl via 1-9
        for (int i = 0; i < 9; i++)
        {
            Keys key = Keys.D1 + i;
            if (keyState.IsKeyDown(key) && _lastKeyState.IsKeyUp(key))
                _selectedSlot = i;
        }

        // Mausrad
        int scrollDelta = scrollWheelValue - _lastScrollValue;
        if (scrollDelta < 0)
            _selectedSlot = (_selectedSlot + 1) % 9;
        else if (scrollDelta > 0)
            _selectedSlot = (_selectedSlot - 1 + 9) % 9;

        _lastScrollValue = scrollWheelValue;
        _lastKeyState = keyState;
    }

    // ── Survival-Pickup (Block abbauen → Inventar) ───────────────────────────

    public bool AddToInventory(BlockType block, int count = 1)
    {
        if (block == BlockType.Air || count <= 0) return false;

        int remaining = count;

        // 1) Existierende Stacks gleichen Typs füllen (erst Hotbar, dann Main)
        for (int i = 0; i < MainEnd && remaining > 0; i++)
        {
            if (_slots[i].Block == block && _slots[i].Count < MaxStack)
            {
                int space = MaxStack - _slots[i].Count;
                int add = Math.Min(space, remaining);
                _slots[i].Count += add;
                remaining -= add;
            }
        }

        // 2) Leere Slots füllen
        for (int i = 0; i < MainEnd && remaining > 0; i++)
        {
            if (_slots[i].IsEmpty)
            {
                _slots[i] = new ItemStack(block, Math.Min(MaxStack, remaining));
                remaining -= _slots[i].Count;
            }
        }

        return remaining < count; // true wenn mindestens 1 Item aufgenommen wurde
    }

    // ── Platzieren verbraucht einen Stack-Count ───────────────────────────────

    public bool ConsumeFromSelected(int count = 1)
    {
        if (_slots[_selectedSlot].IsEmpty) return false;
        _slots[_selectedSlot].Count -= count;
        if (_slots[_selectedSlot].Count <= 0)
            _slots[_selectedSlot] = ItemStack.Empty;
        return true;
    }

    // ── Inventar-Klick-Logik (Minecraft Java Survival) ───────────────────────

    public void OnSlotLeftClick(int slotIdx, bool shift)
    {
        if (!IsInteractiveSlot(slotIdx)) return;

        if (shift)
        {
            ShiftMove(slotIdx);
            return;
        }

        ref ItemStack slot = ref _slots[slotIdx];

        if (_cursorStack.IsEmpty && slot.IsEmpty) return;

        if (_cursorStack.IsEmpty)
        {
            // Ganzen Stack aufnehmen
            _cursorStack = slot;
            slot = ItemStack.Empty;
        }
        else if (slot.IsEmpty)
        {
            // Ganzen Cursor-Stack ablegen
            slot = _cursorStack;
            _cursorStack = ItemStack.Empty;
        }
        else if (slot.Block == _cursorStack.Block)
        {
            // Gleicher Typ → merge
            int space = MaxStack - slot.Count;
            int transfer = Math.Min(space, _cursorStack.Count);
            slot.Count += transfer;
            _cursorStack.Count -= transfer;
            if (_cursorStack.Count <= 0) _cursorStack = ItemStack.Empty;
        }
        else
        {
            // Anderen Typ → tauschen
            (slot, _cursorStack) = (_cursorStack, slot);
        }
    }

    public void OnSlotRightClick(int slotIdx)
    {
        if (!IsInteractiveSlot(slotIdx)) return;

        ref ItemStack slot = ref _slots[slotIdx];

        if (_cursorStack.IsEmpty && slot.IsEmpty) return;

        if (_cursorStack.IsEmpty)
        {
            // Hälfte aufnehmen (aufrunden)
            int pick = (slot.Count + 1) / 2;
            _cursorStack = new ItemStack(slot.Block, pick);
            slot.Count -= pick;
            if (slot.Count <= 0) slot = ItemStack.Empty;
        }
        else if (slot.IsEmpty)
        {
            // Eines ablegen
            slot = new ItemStack(_cursorStack.Block, 1);
            _cursorStack.Count--;
            if (_cursorStack.Count <= 0) _cursorStack = ItemStack.Empty;
        }
        else if (slot.Block == _cursorStack.Block && slot.Count < MaxStack)
        {
            // Eines zum Stack hinzufügen
            slot.Count++;
            _cursorStack.Count--;
            if (_cursorStack.Count <= 0) _cursorStack = ItemStack.Empty;
        }
        else
        {
            // Anderen Typ → tauschen (wie Linksklick)
            (slot, _cursorStack) = (_cursorStack, slot);
        }
    }

    // ── Shift-Klick: Stack zur anderen Region verschieben ────────────────────

    private void ShiftMove(int slotIdx)
    {
        ref ItemStack slot = ref _slots[slotIdx];
        if (slot.IsEmpty) return;

        bool fromHotbar = slotIdx < HotbarEnd;
        int rangeStart  = fromHotbar ? MainStart  : HotbarStart;
        int rangeEnd    = fromHotbar ? MainEnd     : HotbarEnd;

        int remaining = slot.Count;

        // Erst in existierende Stacks gleichen Typs mergen
        for (int i = rangeStart; i < rangeEnd && remaining > 0; i++)
        {
            if (_slots[i].Block == slot.Block && _slots[i].Count < MaxStack)
            {
                int space = MaxStack - _slots[i].Count;
                int add = Math.Min(space, remaining);
                _slots[i].Count += add;
                remaining -= add;
            }
        }

        // Dann in leere Slots
        for (int i = rangeStart; i < rangeEnd && remaining > 0; i++)
        {
            if (_slots[i].IsEmpty)
            {
                int put = Math.Min(MaxStack, remaining);
                _slots[i] = new ItemStack(slot.Block, put);
                remaining -= put;
            }
        }

        slot.Count = remaining;
        if (slot.Count <= 0) slot = ItemStack.Empty;
    }

    // ── Cursor-Stack zurück ins Inventar legen (bei Inventar schließen) ───────

    public void ReturnCursorStack()
    {
        if (_cursorStack.IsEmpty) return;
        AddToInventory(_cursorStack.Block, _cursorStack.Count);
        _cursorStack = ItemStack.Empty;
    }
}
