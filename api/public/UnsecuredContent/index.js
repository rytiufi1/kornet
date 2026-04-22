function getparam(name, url) {
    if (!url) url = window.location.href;
    name = name.replace(/[\[\]]/g, "\\$&");
    var regex = new RegExp("[?&]" + name + "(=([^&#]*)|&|#|$)");
    var results = regex.exec(url);
    if (!results) return null;
    if (!results[2]) return "";
    return decodeURIComponent(results[2].replace(/\+/g, " "));
}

function navigateAfterAuth() {
    var returnUrl = getparam("returnUrl");
    var target = returnUrl && returnUrl.trim() !== "" ? returnUrl : "/home";
    if (window !== window.top) {
        window.parent.postMessage({ type: "navigate", url: target }, "*");
        return;
    }
    window.location.href = target;
}

function getCookie(name) {
    var value = "; " + document.cookie;
    var parts = value.split("; " + name + "=");
    if (parts.length === 2) return parts.pop().split(";").shift();
    return null;
}

function navigateTarget(url) {
    if (window !== window.top) {
        try {
            window.top.location.href = url;
            return;
        } catch (e) {
            window.parent.postMessage({ type: "navigate", url: url }, "*");
            return;
        }
    }
    window.location.href = url;
}

function isOAuthFlow() {
    var returnUrl = getparam("returnUrl") || "";
    return returnUrl.indexOf("/oauth/v1/authorize") !== -1;
}

function getOAuthAuthorizeUrl() {
    var returnUrl = getparam("returnUrl") || "";
    if (returnUrl && returnUrl.indexOf("/oauth/v1/authorize") !== -1) {
        return returnUrl;
    }
    return "/oauth/v1/authorize";
}

function submitLogin(e) {
    e.preventDefault();
    var form = document.getElementById("login-form");
    var loginBtn = document.getElementById("login-button");
    var spinner = document.getElementById("login-spinner");
    if (loginBtn) loginBtn.style.display = "none";
    if (spinner) spinner.style.display = "block";

    var xhr = new XMLHttpRequest();
    var oauthFlow = isOAuthFlow();
    var endpoint = oauthFlow ? "/studio-login/v1/login" : "/login";
    xhr.open("POST", endpoint, true);
    xhr.withCredentials = true;
    if (oauthFlow) {
        xhr.setRequestHeader("Content-Type", "application/json");
    }

    xhr.onload = function() {
        if (loginBtn) loginBtn.style.display = "block";
        if (spinner) spinner.style.display = "none";

        if (oauthFlow && xhr.status >= 200 && xhr.status < 300) {
            navigateTarget(getOAuthAuthorizeUrl());
            return;
        }
        if (document.cookie.includes(".ROBLOSECURITY")) {
            var target = oauthFlow
                ? getOAuthAuthorizeUrl()
                : ((getparam("returnUrl") || "").trim() !== "" ? getparam("returnUrl") : "/home");
            navigateTarget(target);
            return;
        }

        var responseUrl = xhr.responseURL || "";
        if (responseUrl.includes("/home")) {
            var target2 = oauthFlow
                ? getOAuthAuthorizeUrl()
                : ((getparam("returnUrl") || "").trim() !== "" ? getparam("returnUrl") : "/home");
            navigateTarget(target2);
        } else if (responseUrl.includes("/login/2fa") || responseUrl.includes("/login/two-step-verification") || responseUrl.includes("two-step")) {
            navigateTarget("/login/2fa");
        } else if (responseUrl.includes("loginmsg")) {
            var url = new URL(responseUrl);
            var errorMsg = url.searchParams.get("loginmsg");
            if (errorMsg) {
                var errDiv = document.getElementById("login-error-message");
                if (errDiv) {
                    errDiv.textContent = decodeURIComponent(errorMsg);
                    errDiv.style.display = "block";
                }
            }
        }
    };

    xhr.onerror = function() {
        if (loginBtn) loginBtn.style.display = "block";
        if (spinner) spinner.style.display = "none";
    };

    if (oauthFlow) {
        var usernameInput = document.getElementById("login-username");
        var passwordInput = document.getElementById("login-password");
        var body = JSON.stringify({
            username: usernameInput ? usernameInput.value : "",
            password: passwordInput ? passwordInput.value : ""
        });
        xhr.send(body);
    } else {
        var formData = new FormData(form);
        xhr.send(formData);
    }
}

(function() {
    var isInIframe = window !== window.top;
    var returnUrlForFlow = getparam("returnUrl") || "";
    var oauthFlow = returnUrlForFlow.indexOf("/oauth/v1/authorize") !== -1;

    if (isInIframe) {
        try {
            var parentLocation = window.top.location.href;
        } catch (e) {
            return;
        }
    }

    if (document.cookie.includes(".ROBLOSECURITY")) {
        navigateAfterAuth();
        return;
    }

    if (!oauthFlow) {
        var xhr = new XMLHttpRequest();
        xhr.open("GET", "/apisite/users/v1/users/authenticated", true);
        xhr.withCredentials = true;
        xhr.onload = function() {
            if (xhr.status === 200) {
                try {
                    var response = JSON.parse(xhr.responseText);
                    if (response.id && response.name) {
                        navigateAfterAuth();
                    }
                } catch (e) {}
            }
        };
        xhr.onerror = function() {};
        xhr.send();
    }
})();

document.addEventListener("DOMContentLoaded", function() {
    var loginMsg = getparam("loginmsg");
    if (loginMsg && loginMsg.trim() !== "") {
        var errDiv = document.getElementById("login-error-message");
        if (errDiv) {
            errDiv.textContent = loginMsg;
            errDiv.style.display = "block";
        }
    }

    var loginForm = document.getElementById("login-form");
    if (loginForm) {
        loginForm.addEventListener("submit", submitLogin);
    }

    var discordBtn = document.getElementById("facebook-login-button");
    if (discordBtn) {
        discordBtn.addEventListener("click", function(e) {
            e.preventDefault();
            navigateTarget("/login-with-discord");
        });
    }

    var forgotLink = document.getElementById("forgot-credentials-link");
    if (forgotLink) {
        forgotLink.addEventListener("click", function(e) {
            e.preventDefault();
            navigateTarget("/forgotpasswordOrUsername");
        });
    }

    var signupLink = document.getElementById("sign-up-link");
    if (signupLink) {
        signupLink.addEventListener("click", function(e) {
            e.preventDefault();
            navigateTarget("/");
        });
    }
});
