// Session persistence: keeps login session in sync across tabs, page reloads and new windows
window.falimyAuth = {
    setSession: function (userId) {
        if (!userId) return;
        // Cookie valid for 30 days, Lax SameSite so new tab navigation preserves it
        document.cookie = "falimy_user_id=" + encodeURIComponent(userId) + "; path=/; max-age=2592000; SameSite=Lax";
        try {
            localStorage.setItem("falimy_user_id", userId);
        } catch (e) { }
    },
    clearSession: function () {
        document.cookie = "falimy_user_id=; path=/; max-age=0; SameSite=Lax";
        try {
            localStorage.removeItem("falimy_user_id");
        } catch (e) { }
    },
    getSession: function () {
        try {
            var match = document.cookie.match(new RegExp('(^| )falimy_user_id=([^;]+)'));
            if (match && match[2]) return decodeURIComponent(match[2]);
            return localStorage.getItem("falimy_user_id");
        } catch (e) {
            return null;
        }
    }
};
