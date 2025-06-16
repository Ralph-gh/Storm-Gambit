public interface ISpell
{
    string Name { get; }
    bool CanCast(TeamColor casterTeam);
    void Cast(TeamColor casterTeam);
}
