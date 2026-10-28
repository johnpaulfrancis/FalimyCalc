using FalimyCalc.Data;
using FalimyCalc.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace FalimyCalc.Web.Services;

public class UserSessionService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public FamilyMember? CurrentUser { get; private set; }
    public bool IsAuthenticated => CurrentUser is not null;

    public event Action? OnSessionChanged;

    public UserSessionService(IHttpContextAccessor httpContextAccessor, IDbContextFactory<AppDbContext> dbFactory)
    {
        _httpContextAccessor = httpContextAccessor;
        _dbFactory = dbFactory;

        RestoreFromCookie();
    }

    private void RestoreFromCookie()
    {
        try
        {
            var cookie = _httpContextAccessor.HttpContext?.Request.Cookies["falimy_user_id"];
            if (!string.IsNullOrWhiteSpace(cookie) && Guid.TryParse(cookie, out var memberGuid))
            {
                using var db = _dbFactory.CreateDbContext();
                var member = db.FamilyMembers.FirstOrDefault(m => m.GlobalId == memberGuid && !m.IsDeleted);
                if (member != null)
                {
                    CurrentUser = member;
                }
            }
        }
        catch
        {
            // Silently ignore any initialization error during early handshake
        }
    }

    public void Login(FamilyMember member)
    {
        CurrentUser = member;
        OnSessionChanged?.Invoke();
    }

    public void Logout()
    {
        CurrentUser = null;
        OnSessionChanged?.Invoke();
    }
}
