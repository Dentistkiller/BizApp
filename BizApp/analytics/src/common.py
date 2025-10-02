import os
from dotenv import load_dotenv
from sqlalchemy import create_engine
from urllib.parse import quote_plus

load_dotenv()

def sql_engine():
    server = os.getenv("SQL_SERVER")
    db     = os.getenv("SQL_DB")
    user   = os.getenv("SQL_USER")
    pwd    = os.getenv("SQL_PASSWORD")
    auth   = (os.getenv("SQL_AUTH") or "").lower().strip()

    driver = "ODBC Driver 17 for SQL Server"

    if auth == "windows" or not user:
        # Windows Authentication (Trusted_Connection)
        # Note: backslashes in (localdb)\MSSQLLocalDB are fine here.
        params = f"driver={quote_plus(driver)}&trusted_connection=yes"
        uri = f"mssql+pyodbc://@{server}/{db}?{params}"
    else:
        # SQL Authentication
        params = f"driver={quote_plus(driver)}"
        uri = f"mssql+pyodbc://{quote_plus(user)}:{quote_plus(pwd)}@{server}/{db}?{params}"

    return create_engine(uri, fast_executemany=True)
