

const API_URL = "https://localhost:7243";
const token = sessionStorage.getItem("token");

if (!token) {
    window.location.replace("index.html");
}

// Logout
document.getElementById("logoutBtn").addEventListener("click", () => {
    sessionStorage.removeItem("token");
    window.location.replace("index.html");
});

// Reset form
function resetProductForm() {
    document.getElementById("addProductForm").reset();
    document.getElementById("editingProductID").value = "";
    document.getElementById("productFormTitle").textContent =
        "Add New Product";
    document.getElementById("saveProductBtn").textContent =
        "Add Product";
    document.getElementById("cancelEditBtn").style.display =
        "none";
}

// Cancel editing
document.getElementById("cancelEditBtn")
    .addEventListener("click", () => {
        resetProductForm();
        document.getElementById("addProductMessage").textContent = "";
    });

// Load all products
async function loadProducts() {
    if (!token) return;

    const tbody = document.getElementById("productsBody");
    const message = document.getElementById("productsMessage");

    message.textContent = "";

    try {
        const response = await fetch(`${API_URL}/api/Products`, {
            headers: {
                "Authorization": `Bearer ${token}`
            }
        });

        if (!response.ok) {
            throw new Error(
                `Failed to load products (${response.status})`
            );
        }

        const products = await response.json();

        tbody.replaceChildren();

        if (products.length === 0) {
            const row = tbody.insertRow();
            const cell = row.insertCell();

            cell.colSpan = 8;
            cell.textContent = "No products found.";
            return;
        }

        products.forEach(product => {
            const row = tbody.insertRow();

            // Product details
            const values = [
                product.productID,
                product.productName,
                product.sku,
                "₹" + Number(product.wholesalePrice)
                    .toLocaleString("en-IN"),
                product.stockQuantity,
                product.minOrderQuantity,
                product.maxOrderQuantity
            ];

            values.forEach(value => {
                const cell = row.insertCell();
                cell.textContent = value;
            });

            // Action column
            const actionCell = row.insertCell();

            const editButton = document.createElement("button");
            editButton.type = "button";
            editButton.textContent = "Edit";

            editButton.addEventListener("click", () => {
                editProduct(product);
            });

            actionCell.appendChild(editButton);
        });

    } catch (error) {
        message.textContent = error.message;
        console.error(error);
    }
}

// Fill form with selected product
function editProduct(product) {
    document.getElementById("editingProductID").value =
        product.productID;

    document.getElementById("categoryID").value =
        product.categoryID;

    document.getElementById("supplierID").value =
        product.supplierID;

    document.getElementById("productName").value =
        product.productName;

    document.getElementById("sku").value =
        product.sku;

    document.getElementById("wholesalePrice").value =
        product.wholesalePrice;

    document.getElementById("minOrderQuantity").value =
        product.minOrderQuantity;

    document.getElementById("maxOrderQuantity").value =
        product.maxOrderQuantity;

    document.getElementById("productFormTitle").textContent =
        "Edit Product";

    document.getElementById("saveProductBtn").textContent =
        "Update Product";

    document.getElementById("cancelEditBtn").style.display =
        "inline-block";

    document.getElementById("addProductMessage").textContent = "";

    window.scrollTo({ top: 0, behavior: "smooth" });
}

// Add or Update product
document.getElementById("addProductForm")
    .addEventListener("submit", async function (event) {
        event.preventDefault();

        if (!token) {
            window.location.replace("index.html");
            return;
        }

        const message =
            document.getElementById("addProductMessage");

        const editingID =
            document.getElementById("editingProductID").value;

        const isEditing = editingID !== "";

        const product = {
            categoryID: Number(
                document.getElementById("categoryID").value
            ),
            supplierID: Number(
                document.getElementById("supplierID").value
            ),
            productName: document.getElementById("productName")
                .value.trim(),
            sku: document.getElementById("sku").value.trim(),
            wholesalePrice: Number(
                document.getElementById("wholesalePrice").value
            ),
            stockQuantity: 0,
            minOrderQuantity: Number(
                document.getElementById("minOrderQuantity").value
            ),
            maxOrderQuantity: Number(
                document.getElementById("maxOrderQuantity").value
            )
        };

        // Validate quantities
        if (product.maxOrderQuantity < product.minOrderQuantity) {
            message.textContent =
                "Maximum order quantity must be greater than or equal to minimum.";
            return;
        }

        const saveButton =
            document.getElementById("saveProductBtn");

        saveButton.disabled = true;
        message.textContent = "Saving product...";

        try {
            const url = isEditing
                ? `${API_URL}/api/Products/${editingID}`
                : `${API_URL}/api/Products`;

            const response = await fetch(url, {
                method: isEditing ? "PUT" : "POST",
                headers: {
                    "Content-Type": "application/json",
                    "Authorization": `Bearer ${token}`
                },
                body: JSON.stringify(product)
            });

            if (!response.ok) {
                message.textContent =
                    `Failed to save product (${response.status}).`;
                return;
            }

            resetProductForm();

            message.textContent = isEditing
                ? "Product Updated Successfully!"
                : "Product Added Successfully!";

            await loadProducts();

        } catch (error) {
            message.textContent = "Cannot connect to the API.";
            console.error(error);

        } finally {
            saveButton.disabled = false;
        }
    });

// Load products when page opens
loadProducts();

