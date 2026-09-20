using System;

namespace PKHeX.Core;

/// <summary>
/// Personal Table storing <see cref="PersonalInfo3"/> used in Generation 3 games.
/// </summary>
public sealed class PersonalTable3 : IPersonalTable, IPersonalTable<PersonalInfo3>
{
    private readonly PersonalInfo3[] Table;
    private const int SIZE = PersonalInfo3.SIZE;
    private const ushort MaxSpecies = Legal.MaxSpeciesID_3;
    public ushort MaxSpeciesID => MaxSpecies;
    public int Count => Table.Length;

    public PersonalTable3(Memory<byte> data)
    {
        var count = data.Length / SIZE;
        Table = new PersonalInfo3[count + 3];
        for (int i = 0, ofs = 0; i < count; i++, ofs += SIZE)
        {
            var slice = data.Slice(ofs, SIZE);
            Table[i] = new PersonalInfo3(slice);
        }
        
        var baseDeoxys = Table[386].Write();
        
        var attackData = (byte[])baseDeoxys.Clone();
        attackData[0] = 50; attackData[1] = 180; attackData[2] = 20; attackData[3] = 150; attackData[4] = 180; attackData[5] = 20;
        Table[count] = new PersonalInfo3(attackData);

        var defenseData = (byte[])baseDeoxys.Clone();
        defenseData[0] = 50; defenseData[1] = 70; defenseData[2] = 160; defenseData[3] = 90; defenseData[4] = 70; defenseData[5] = 160;
        Table[count + 1] = new PersonalInfo3(defenseData);

        var speedData = (byte[])baseDeoxys.Clone();
        speedData[0] = 50; speedData[1] = 95; speedData[2] = 90; speedData[3] = 180; speedData[4] = 95; speedData[5] = 90;
        Table[count + 2] = new PersonalInfo3(speedData);
    }

    public PersonalInfo3 this[int index] => Table[(uint)index < Table.Length ? index : 0];
    public PersonalInfo3 this[ushort species, byte form] => Table[GetFormIndex(species, form)];
    public PersonalInfo3 GetFormEntry(ushort species, byte form) => Table[GetFormIndex(species, form)];

    public int GetFormIndex(ushort species, byte form)
    {
        if (species == (int)Species.Deoxys && form > 0 && form < 4)
            return Table.Length - 4 + form;
        return IsSpeciesInGame(species) ? species : 0;
    }
    public bool IsSpeciesInGame(ushort species) => species <= MaxSpecies;
    public bool IsPresentInGame(ushort species, byte form)
    {
        if (!IsSpeciesInGame(species))
            return false;
        return form == 0 || species switch
        {
            (int)Species.Unown => form < 28,
            (int)Species.Castform => form < 4,
            (int)Species.Deoxys => form < 4,
            _ => false,
        };
    }

    PersonalInfo IPersonalTable.this[int index] => this[index];
    PersonalInfo IPersonalTable.this[ushort species, byte form] => this[species, form];
    PersonalInfo IPersonalTable.GetFormEntry(ushort species, byte form) => GetFormEntry(species, form);

    internal void LoadTables(BinLinkerAccessor machine, BinLinkerAccessor tutors)
    {
        var table = Table;
        for (int i = Legal.MaxSpeciesID_3; i >= 1; i--)
        {
            var entry = table[i];
            entry.AddTMHM(machine[i]);
            entry.AddTypeTutors(tutors[i]);
        }
    }

    internal void CopyTables(PersonalTable3 pt)
    {
        // Copy to other tables
        var other = pt.Table;
        var table = Table;
        for (int i = Legal.MaxSpeciesID_3; i >= 1; i--)
            table[i].CopyFrom(other[i]);
    }
}
