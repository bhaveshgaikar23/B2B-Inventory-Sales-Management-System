
const token = sessionStorage.getItem("token");

if (!token) {
    window.location.replace("index.html");
}

document.getElementById("logoutBtn").addEventListener("click", function () {
    sessionStorage.removeItem("token");
    window.location.replace("index.html");
});

const API_URL = "https://localhost:7243";

async function loadDashboardStats() {
    const token = sessionStorage.getItem("token");

    if (!token) return;

    try {
        const response = await fetch(
            `${API_URL}/api/Products`,
            {
                headers: {
                    "Authorization": `Bearer ${token}`
                }
            }
        );

        if (!response.ok) {
            throw new Error("Failed to load products");
        }

        const products = await response.json();

        document.getElementById("totalProducts").textContent =
            products.length;
        const salesResponse = await fetch(
            `${API_URL}/api/Reports/sales-summary`,
            {
                headers: {
                    "Authorization": `Bearer ${token}`
                }
            }
        );

        if (!salesResponse.ok) {
            throw new Error("Failed to load sales summary");
        }

        const salesData = await salesResponse.json();

        console.log("Sales Summary:", salesData);
        document.getElementById("totalOrders").textContent =
            salesData.totalOrders;

        document.getElementById("pendingOrders").textContent =
            salesData.pendingOrders;

        document.getElementById("totalSales").textContent =
            "₹" + Number(salesData.totalSales).toLocaleString("en-IN");

    } catch (error) {
        console.error("Dashboard error:", error);
        document.getElementById("totalProducts").textContent =
            "Error";
    }
}

loadDashboardStats();
