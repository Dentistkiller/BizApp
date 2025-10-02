# VeriPay — Real-time Fraud Detection & Transaction Monitoring

**VeriPay** is a full-stack, production-ready sample that demonstrates **real-time payment fraud detection** with a modern ASP.NET Core MVC app (EF Core + SQL Server) and a Python analytics pipeline (pandas / scikit-learn / XGBoost). It ingests and scores transactions, provides analyst workflows to review/label, and ships with role-based access (Admin vs Customer).

* **End-to-end**: ASP.NET Core MVC UI + EF Core + SQL Server schema + Python ML.
* **Real-time-ish scoring**: new transactions are immediately scored via a Python worker.
* **Analyst tools**: dashboard KPIs, top merchants, daily flagged series, transaction drill-downs, human labels.
* **RBAC**: Admin users see all; Customers see only their own transactions & cards.
* **Privacy-by-design**: emails/phones are **hashed**, model reasons recorded as JSON, timestamped events.
* **Batteries included**: schema migrations, seed scripts, Kaggle ingestion, training, metrics & model registry.


With VeriPay’s architecture, every transaction gets a risk score and (optionally) model reasons, and can be overridden by analysts. The dashboard surfaces the latest trends and lets you tune your data pipeline end-to-end.

---

## Contents

* [Features](#features)
* [Architecture](#architecture)
* [Data Model](#data-model)
* [Setup](#setup)

  * [Prerequisites](#prerequisites)
  * [Environment Variables](#environment-variables)
  * [Database Migrations & Seed](#database-migrations--seed)
  * [Run the App](#run-the-app)
* [Python Analytics](#python-analytics)

  * [Kaggle Ingest](#kaggle-ingest)
  * [Feature Engineering](#feature-engineering)
  * [Training & Model Registry](#training--model-registry)
  * [Scoring a Single Transaction](#scoring-a-single-transaction)
  * [Metrics](#metrics)
* [ASP.NET App](#aspnet-app)

  * [Authentication & Roles](#authentication--roles)
  * [Pages & Flows](#pages--flows)
  * [Security Notes](#security-notes)
* [Admin Playbook](#admin-playbook)
* [Dev Tips & Troubleshooting](#dev-tips--troubleshooting)
* [Roadmap](#roadmap)
* [License](#license)

---

## Features

* **Dashboard**

  * Tx (24h), Flagged (24h), Flag rate, Amount (24h),
  * Daily totals vs flagged (Chart.js),
  * Top merchants by flagged rate (windowed; min counts).
* **Transactions**

  * Create/Read/Edit/Delete (CRUD), details with score & reasons,
  * Label Fraud/Legit; analyst override back-propagates into `ml.TxScores`.
* **Merchants**

  * CRUD; optional SQL script to replace placeholder names with real ZA brands.
* **Customers**

  * Register/Login using **hashed** email/phone,
  * Customer portal to **view/update profile**, **list cards**, **create a new card**.
* **RBAC**

  * `Admin` role: full visibility (all transactions),
  * `Customer`: restricted to own cards & transactions.
* **Python ML**

  * Kaggle credit card fraud dataset ingest (ULB),
  * Feature engineering (velocity features, calendar, categorical encodings),
  * Model training (Logistic + Calibrated, XGBoost; picks best by PR-AUC),
  * Model registry table + metrics table + serialized model bundle (joblib),
  * One-off scoring script invoked by the web app on new transaction create.

---

## Architecture

```
+-------------------+        Create Tx        +-------------------+
|  ASP.NET MVC UI   |  -------------------->  |   SQL Server DB   |
|  (EF Core)        |                         | ops/ml schemas    |
|  Auth, RBAC       |  <--------------------  |  TxScores, Labels |
|  Dashboard, CRUD  |      Query results      |  Metrics, Runs    |
+---------+---------+                         +---------+---------+
          |                                             ^
          | spawn python                                |
          v                                             |
+-------------------+         read/write                |
|  Python Analytics |  -------------------------------> |
|  pandas, sklearn  |   features, train, score          |
|  xgboost, joblib  |   (via SQLAlchemy + pyodbc)       |
+-------------------+                                   |
```

**Key Design Notes**

* Time fields such as `ops.Transactions.tx_utc` and `ml.Labels.labeled_at` are stored as **UTC strings** (`"yyyy-MM-dd HH:mm:ss"`) to avoid cross-stack timezone casting issues; comparisons are **lexicographical** in SQL and parsed in-memory for charts.
* Fraud labels are written by analysts and **OR-combined** with model flags on dashboard counters.
* New transaction creation kicks off **`score_one.py`** in a background process; results are written to `ml.TxScores`.

---

## Data Model

Schemas (truncated for clarity):

* **ops.Transactions**

  * `tx_id` (PK), `customer_id`, `card_id`, `merchant_id`
  * `amount` (DECIMAL), `currency` (NVARCHAR), `tx_utc` (**NVARCHAR UTC string**)
  * `entry_mode`, `channel`, `device_id_hash` (VARBINARY), `ip_hash` (VARBINARY)
  * `lat`, `lon`, `status`
* **ops.Customers**

  * `customer_id` (PK), `name`, `email_hash` (VARBINARY), `phone_hash` (VARBINARY),
  * `password_hash` (VARBINARY), `password_salt` (VARBINARY), `created_at` (DATETIME)
* **ops.Cards**

  * `card_id` (PK), `customer_id`, `network`, `last4`, `issue_country`, …
* **ops.Merchants**

  * `merchant_id` (PK), `name`, `category`, `country`, `risk_level`, `created_at`
* **ml.TxScores**

  * `tx_id` (PK/FK), `score` (FLOAT), `label_pred` (BIT), `reason_json` (NVARCHAR(MAX))
* **ml.Labels**

  * `tx_id` (PK/FK), `label` (BIT), `labeled_at` (**NVARCHAR UTC string**), `source` (NVARCHAR)
* **ml.Runs / ml.Metrics**

  * Model runs registry and scalar metrics (PR-AUC, ROC-AUC, recall@k, confusion).

> EF Core models mirror these tables and enforce RBAC in controllers.

---

## Setup

### Prerequisites

* **.NET 8 SDK**
* **Python 3.11+** (pandas, scikit-learn, xgboost, SQLAlchemy, pyodbc, joblib)
* **SQL Server** (Developer/Express/Azure SQL)
* **Kaggle CLI** (optional; for dataset ingestion)

```bash
# Python deps (example)
pip install pandas scikit-learn xgboost sqlalchemy pyodbc joblib
# Kaggle (optional)
pip install kaggle
```

### Environment Variables

Create a `.env` or set in your shell:

```
# ASP.NET
ASPNETCORE_ENVIRONMENT=Development

# SQL
VERIPAY_SQL_CONN="Server=localhost;Database=VeriPay;Trusted_Connection=True;TrustServerCertificate=True"

# Python model IO
MODEL_PATH=models/model_kaggle.pkl
MODEL_VERSION=kaggle_v1

# Kaggle (optional if using ingest)
KAGGLE_USERNAME=your_user
KAGGLE_KEY=your_api_key
```

> The app and Python code both read the SQL connection string.
> In C#, it comes from `appsettings.json` or `VERIPAY_SQL_CONN`.
> In Python, via `common.sql_engine()`; set your env accordingly.

### Database Migrations & Seed

```bash
# from the Web project directory
dotnet ef database update
```

Optionally seed:

* **Admin user**:

  ```sql
  -- minimal seed for admin role (email: admin@admin.com)
  INSERT INTO ops.Customers (customer_id, name, created_at)
  VALUES (1, 'Admin', SYSUTCDATETIME());

  INSERT INTO ops.Roles (role_name) VALUES ('Admin'); -- if you use a roles table
  INSERT INTO ops.CustomerRoles (customer_id, role_name) VALUES (1, 'Admin');
  ```

* **ZA Merchant rename example**: see `scripts/seed_merchants_za.sql` (sample provided in earlier step).

### Run the App

```bash
# terminal 1: web
dotnet run --project BizApp

# terminal 2: (optional) ingest + train
cd BizApp/analytics
python src/run_all.py           # downloads Kaggle, ingests, trains, registers model
```

Open **[https://localhost:7219/](https://localhost:7219/)**

---

## Python Analytics

### Kaggle Ingest

`analytics/src/ingest_kaggle.py`:

* Downloads **ULB credit card fraud dataset** via Kaggle API,
* Normalizes columns to match `ops.Transactions` (e.g., `tx_utc` **as UTC string**),
* Inserts rows via **SQLAlchemy + pyodbc** into `ops.Transactions`.

Run:

```bash
cd BizApp/analytics
python src/ingest_kaggle.py
```

Common options: set `VERIPAY_SQL_CONN`, ensure ODBC Driver 17 installed.

### Feature Engineering

`analytics/src/features.py`:

* **_prep**: parse `tx_utc` to UTC (in-memory), coerce amount, normalize categorical `channel/entry_mode/currency`.
* **_time_features**: hour, day-of-week, weekend.
* **_amount_features**: log(amount).
* **_velocity_features**: rolling counts & sums over **24h/7d** at card & customer levels, plus “new card/merchant” flags.
* **_encode_cats**: one-hot encode categorical fields with `dummy_na=True`.

The velocity windows use pandas `groupby().rolling(window='24H')` style. We changed `.clip(lower=0)` to use numpy’s scalar `np.maximum` where needed to avoid dtype issues.

### Training & Model Registry

`analytics/src/train_kaggle.py`:

* **Temporal split**: last 14 days held out (`tx_utc` sorted; parsed to datetime in-memory),
* Trains **Calibrated Logistic Regression** and **XGBoost**,
* Picks best by **PR-AUC**,
* Computes: PR-AUC, ROC-AUC, recall@3%, confusion@threshold,
* Inserts a new row in **`ml.Runs`**, writes per-metric rows in **`ml.Metrics`**,
* Serializes a **bundle** (joblib) with `{ model, which, threshold, features, metrics }` to `MODEL_PATH`.

```bash
python src/train_kaggle.py
```

### Scoring a Single Transaction

`analytics/src/score_one.py <tx_id>`:

* Pulls the transaction,
* Recomputes features (mirror training pipeline),
* Loads latest model bundle,
* Writes to **`ml.TxScores`**: `{ tx_id, score, label_pred, reason_json }`.

The ASP.NET `TransactionsController.Create` invokes this script right after inserting a transaction (non-blocking process call) so the **score appears immediately** in the UI upon refresh.

```bash
python src/score_one.py 630235
```

### Metrics

Standard helpers in `analytics/src/metrics.py`:

* `pr_auc`, `roc_auc`
* `pick_threshold_by_flag_rate(y_score, 0.03)`
* `confusion_at_threshold`
* `recall_at_top_k`

Metrics are written to `ml.Metrics` keyed by `run_id`.

---

## ASP.NET App

### Authentication & Roles

* **Register**: `AuthController.Register` creates `ops.Customers` where:

  * `email_hash`, `phone_hash` are **SHA-256** of normalized values,
  * Passwords use per-user random salt + **PBKDF2** (or equivalent) → `password_hash`.
* **Login**: `AuthController.Login` finds by **email hash**, verifies password hash,
  issues a **cookie** with claims: `NameIdentifier` (customer_id), `Name`, `Role` (`Admin` if present).
* **[Authorize]**: controllers / actions are protected.
  Admin-only requires `[Authorize(Roles="Admin")]`.
  Otherwise, controllers filter queries by `customer_id` from claims.

### Pages & Flows

* **Home**: polished landing page for VeriPay.
* **Dashboard (Admin only)**:

  * KPIs (24h), Chart.js “Daily totals vs flagged”, Top merchants.
  * **String timestamps**: filters use `string.Compare(tx_utc, sinceStr) >= 0` server-side; parse to `DateTime` in memory for chart buckets.
* **Transactions**:

  * **Index**: filter by date range, merchant, flagged only; Admin sees all, Customers see **only their own**.
  * **Details**: score, label, reasons; “Mark Fraud/Legit” writes to `ml.Labels` (string timestamp) and syncs `ml.TxScores`.
  * **Create**: pre-fills `tx_utc` (UTC), **geolocation** (lat/lon), **device_id_hash** and **ip_hash** (client-side SHA-256 hex → server converts to VARBINARY). After insert → **score one**.
* **Merchants**: CRUD
* **Customers**:

  * Customer portal at **/Auth/Index** with profile (update name/phone) + cards table + **Create card** form.

### Security Notes

* Never store raw email/phone; **hash only**.
* Passwords are **salted & hashed** (no plaintext).
* Enforce HTTPS and cookie `Secure` flag.
* Avoid direct casting between DB datetimes and app datetimes: we store **UTC strings** to keep C#/Python/SQL consistent.
* For IP/device hashing in browser: we send hex to server and convert to VARBINARY; server also computes a fallback hash if fields missing.

---

## Admin Playbook

* **Grant Admin**:

  * Assign role record for a customer (depends on your role table/claim logic).
* **Replace merchant placeholders**:

  * Run `scripts/seed_merchants_za.sql` which maps “Merchant N” to recognized ZA brands in each category (Fuel/Retail/Grocery/Electronics/Online).
* **Retrain**:

  ```bash
  cd BizApp/analytics
  python src/run_all.py     # or python src/train_kaggle.py
  ```
* **Inspect metrics**:

  * Browse **Model Runs** & **Metrics** pages (if scaffolded), or query `ml.Metrics`.

---

## Roadmap

* ✅ Role-based access & customer portal
* ✅ Analyst override sync ↔ `ml.TxScores`
* ✅ Daily series + Top merchants
* ⏳ Background queue for scoring (e.g., Hangfire/SQL Agent)
* ⏳ Model version pinning per environment
* ⏳ SHAP values for feature attributions
* ⏳ Batch re-scoring job and drift monitoring
* ⏳ API endpoints for external ingest

---

## License

This sample is provided under the **MIT License**.
Use it as a starting point for your fraud detection prototypes and internal demos.

---

### Appendix: Useful Commands

```bash
# Web
dotnet restore
dotnet ef database update
dotnet run --project BizApp

# Python
cd BizApp/analytics
python -m venv .venv && . .venv/Scripts/activate
pip install -r requirements.txt  # if present; otherwise install libs listed above
python src/ingest_kaggle.py
python src/train_kaggle.py
python src/score_one.py 123456
```
