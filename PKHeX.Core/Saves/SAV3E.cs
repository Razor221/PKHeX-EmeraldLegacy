using System;
using System.Collections.Generic;
using static System.Buffers.Binary.BinaryPrimitives;

namespace PKHeX.Core;

/// <summary>
/// Generation 3 <see cref="SaveFile"/> object for <see cref="GameVersion.E"/>.
/// </summary>
/// <inheritdoc cref="SAV3" />
public sealed class SAV3E : SAV3, IDaycareRandomState<uint>
{
    // Configuration
    protected override SAV3E CloneInternal() => new(GetFinalData()) { Language = Language };
    public override SaveBlock3SmallE SmallBlock { get; }
    public override SaveBlock3LargeE LargeBlock { get; }
    public override GameVersion Version { get => GameVersion.E; set { } }
    public override PersonalTable3 Personal => PersonalTable.E;

    public SAV3E(Memory<byte> data) : base(data)
    {
        SmallBlock = new SaveBlock3SmallE(SmallBuffer[..0xF2C]);
        LargeBlock = new SaveBlock3LargeE(LargeBuffer[..0x3D88]);
    }
    public SAV3E(bool japanese = false) : base(japanese)
    {
        SmallBlock = new SaveBlock3SmallE(SmallBuffer[..0xF2C]);
        LargeBlock = new SaveBlock3LargeE(LargeBuffer[..0x3D88]);
    }

    public override PlayerBag3E Inventory => new(this);
    public override int MaxItemID => Legal.MaxItemID_3_E;

    // storage
    private void Initialize() => Box = 0;

    #region Small
    public override bool NationalDex
    {
        get => SmallBlock.PokedexNationalMagicRSE == PokedexNationalUnlockRSE;
        set
        {
            SmallBlock.PokedexMode = value ? (byte)1 : (byte)0; // mode
            SmallBlock.PokedexNationalMagicRSE = value ? PokedexNationalUnlockRSE : (byte)0; // magic
            SetEventFlag(0x896, value);
            SetWork(0x46, PokedexNationalUnlockWorkRSE);
        }
    }

    #endregion

    #region Large

    public override uint Money
    {
        get => LargeBlock.Money ^ SmallBlock.SecurityKey;
        set => LargeBlock.Money = value ^ SmallBlock.SecurityKey;
    }

    public override uint Coin
    {
        get => (ushort)(LargeBlock.Coin ^ SmallBlock.SecurityKey);
        set => LargeBlock.Coin = (ushort)(value ^ SmallBlock.SecurityKey);
    }

    private const int OFS_PCItem = 0x0498;
    private const int OFS_PouchHeldItem = 0x0560;
    private const int OFS_PouchKeyItem = 0x0740;
    private const int OFS_PouchBalls = 0x07B8;
    private const int OFS_PouchTMHM = 0x07F8;
    private const int OFS_PouchBerry = 0x08F8;
    private const int OFS_BerryBlenderRecord = 0xB24;
    private const int OFS_TrendyWord = 0x2F88;
    private const int OFS_TrainerHillRecord = 0x3718;

    private Span<byte> PokeBlockData => Large.AsSpan(0x9B0, PokeBlock3Case.SIZE);

    public PokeBlock3Case PokeBlocks
    {
        get => new(PokeBlockData);
        set => value.Write(PokeBlockData);
    }

    public DecorationInventory3 Decorations => new(Large.AsSpan(0x289C, DecorationInventory3.SIZE));

    private Span<byte> SwarmSpan => Large.AsSpan(0x2CF8, Swarm3.SIZE);
    public Swarm3 Swarm
    {
        get => new(SwarmSpan.ToArray());
        set => SetData(SwarmSpan, value.Data);
    }

    private void ClearSwarm() => SwarmSpan.Clear();

    public IReadOnlyList<Swarm3> DefaultSwarms => Swarm3Details.Swarms_E;

    public int SwarmIndex
    {
        get => Array.FindIndex(Swarm3Details.Swarms_E, z => z.MapNum == Swarm.MapNum);
        set
        {
            var arr = DefaultSwarms;
            if ((uint)value >= arr.Count)
                ClearSwarm();
            else
                Swarm = arr[value];
        }
    }

    protected override int GetDaycareEXPOffset(int slot) => GetDaycareSlotOffset(slot + 1) - 4; // @ end of each pk slot
    uint IDaycareRandomState<uint>.Seed // after the 2 slots, before the step counter
    {
        get => LargeBlock.DaycareSeed;
        set => LargeBlock.DaycareSeed = value;
    }

    /// <summary>
    /// Max RPM for 2, 3 and 4 players. Each value unit represents 0.01 RPM. Value 0 if no record.
    /// </summary>
    /// <remarks>2 players: index 0, 3 players: index 1, 4 players: index 2</remarks>
    public const int BerryBlenderRPMRecordCount = 3;

    private Span<byte> GetBlenderRPMSpan(int index)
    {
        if ((uint)index >= BerryBlenderRPMRecordCount)
            throw new ArgumentOutOfRangeException(nameof(index));
        return Large.AsSpan(OFS_BerryBlenderRecord + (index * 2));
    }

    public ushort GetBerryBlenderRPMRecord(int index) => ReadUInt16LittleEndian(GetBlenderRPMSpan(index));

    public void SetBerryBlenderRPMRecord(int index, ushort value)
    {
        WriteUInt16LittleEndian(GetBlenderRPMSpan(index), value);
        State.Edited = true;
    }

    public bool GetTrendyWordUnlocked(TrendyWord3E word)
    {
        return GetFlag(OFS_TrendyWord + ((byte)word >> 3), (byte)word & 7);
    }

    public void SetTrendyWordUnlocked(TrendyWord3E word, bool value)
    {
        SetFlag(OFS_TrendyWord + ((byte)word >> 3), (byte)word & 7, value);
        State.Edited = true;
    }

    /** Each value unit represents 1/60th of a second. Value 0 if no record. */
    public uint GetTrainerHillRecord(TrainerHillMode3E mode)
    {
        return ReadUInt32LittleEndian(Large.AsSpan(OFS_TrainerHillRecord + ((byte)mode * 4)));
    }

    public void SetTrainerHillRecord(TrainerHillMode3E mode, uint value)
    {
        WriteUInt32LittleEndian(Large.AsSpan(OFS_TrainerHillRecord + ((byte)mode * 4)), value);
        State.Edited = true;
    }

    #region eBerry
    private const int OFFSET_EBERRY = 0x3360;
    private const int SIZE_EBERRY = 0x34;
    #endregion

    #region eTrainer
    #endregion

    public int WonderOffset => WonderNewsOffset;
    private const int WonderNewsOffset = 0x3394;
    private int WonderCardOffset => WonderNewsOffset + (Japanese ? WonderNews3.SIZE_JAP : WonderNews3.SIZE);
    private int WonderCardExtraOffset => WonderCardOffset + (Japanese ? WonderCard3.SIZE_JAP : WonderCard3.SIZE);

    private Span<byte> WonderNewsData => Large.AsSpan(WonderNewsOffset, Japanese ? WonderNews3.SIZE_JAP : WonderNews3.SIZE);
    private Span<byte> WonderCardData => Large.AsSpan(WonderCardOffset, Japanese ? WonderCard3.SIZE_JAP : WonderCard3.SIZE);
    private Span<byte> WonderCardExtraData => Large.AsSpan(WonderCardExtraOffset, WonderCard3Extra.SIZE);

    public WonderNews3 WonderNews { get => new(WonderNewsData.ToArray()); set => SetData(WonderNewsData, value.Data); }
    public WonderCard3 WonderCard { get => new(WonderCardData.ToArray()); set => SetData(WonderCardData, value.Data); }
    public WonderCard3Extra WonderCardExtra { get => new(WonderCardExtraData.ToArray()); set => SetData(WonderCardExtraData, value.Data); }
    // 0x338: 4 easy chat words
    // 0x340: news MENewsJisanStruct
    // 0x344: uint[5], uint[5] tracking?

    private Span<byte> MysterySpan => Large.AsSpan(0x3728, MysteryEvent3.SIZE);

    private Span<byte> RecordMixingData => Large.AsSpan(0x3B14, RecordMixing3Gift.SIZE);
    public RecordMixing3Gift RecordMixingGift { get => new(RecordMixingData.ToArray()); set => SetData(RecordMixingData, value.Data); }

    private const int Walda = 0x3D70;
    public ushort WaldaBackgroundColor { get => ReadUInt16LittleEndian(Large.AsSpan(Walda + 0)); set => WriteUInt16LittleEndian(Large.AsSpan(Walda + 0), value); }
    public ushort WaldaForegroundColor { get => ReadUInt16LittleEndian(Large.AsSpan(Walda + 2)); set => WriteUInt16LittleEndian(Large.AsSpan(Walda + 2), value); }
    public byte WaldaIconID { get => Large[Walda + 0x14]; set => Large[Walda + 0x14] = value; }
    public byte WaldaPatternID { get => Large[Walda + 0x15]; set => Large[Walda + 0x15] = value; }
    public bool WaldaUnlocked { get => Large[Walda + 0x16] != 0; set => Large[Walda + 0x16] = (byte)(value ? 1 : 0); }

    private Memory<byte> SecretBaseData => Large.AsMemory(0x1C04, SecretBaseManager3.BaseCount * SecretBase3.SIZE);
    public SecretBaseManager3 SecretBases => new(SecretBaseData);

    private const int Painting = 0x2F90;
    private const int CountPaintings = 5;
    private Span<byte> GetPaintingSpan(int index)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, CountPaintings, nameof(index));
        return Large.AsSpan(Painting + (Paintings3.SIZE * index), Paintings3.SIZE * CountPaintings);
    }
    public Paintings3 GetPainting(int index) => new(GetPaintingSpan(index).ToArray(), Japanese);
    public void SetPainting(int index, Paintings3 value) => value.Data.CopyTo(GetPaintingSpan(index));
    #endregion

    private const uint EXTRADATA_SENTINEL = 0x0000B39D;
    public bool HasBattleVideo => IsFullSaveFile && ReadUInt32LittleEndian(GetFinalExternalData().Span) == EXTRADATA_SENTINEL;

    public void SetExtraDataSentinelBattleVideo() => WriteUInt32LittleEndian(GetFinalExternalData().Span, EXTRADATA_SENTINEL);

    public Memory<byte> BattleVideoData => GetFinalExternalData().Slice(4, BattleVideo3.SIZE);
    public BattleVideo3 BattleVideo
    {
        // decouple from the save file object on get, as the consumer might not be aware that mutations will touch the save.
        get => HasBattleVideo ? new BattleVideo3(BattleVideoData.ToArray()) : new BattleVideo3();
        set => SetData(BattleVideoData.Span, value.Data);
    }
}
