
const API_URL = "https://localhost:7243";

document.getElementById("loginForm").addEventListener("submit", async function (event) {
    event.preventDefault();

    const email = document.getElementById("email").value.trim();
    const password = document.getElementById("password").value;
    const message = document.getElementById("message");

    message.textContent = "Logging in...";

    try {
        const response = await fetch(`${API_URL}/api/Users/login`, {
            method: "POST",
            headers: {
                "Content-Type": "application/json"
            },
            body: JSON.stringify({
                email: email,
                password: password
            })
        });

        const data = await response.json();

        if (response.ok) {
            if (!data.token) {
                message.textContent = "Login response does not contain a token.";
                return;
            }

            sessionStorage.setItem("token", data.token);

            if (data.role === "Admin") {
                window.location.href = "admin-dashboard.html";
            }
            else if (data.role === "Shopkeeper") {
                window.location.href = "shopkeeper-dashboard.html";
            }
            else {
                sessionStorage.removeItem("token");
                message.textContent = "Unknown user role.";
            }
        } else {
            message.textContent = data.message || "Invalid email or password.";
        }
    } catch (error) {
        message.textContent = "Cannot connect to the API.";
        console.error(error);
    }
});
