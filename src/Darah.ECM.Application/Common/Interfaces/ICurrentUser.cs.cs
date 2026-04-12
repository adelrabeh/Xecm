namespace Darah.ECM.Application.Common.Interfaces;

public interface ICurrentUser
{
    int UserId { get; }
    string UserName { get; }
    string Email { get; }
    string DisplayNameAr { get; }
    string DisplayNameEn { get; }
    string Department { get; }
    bool IsAuthenticated { get; }
}