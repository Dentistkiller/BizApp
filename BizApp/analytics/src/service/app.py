import os
import json
import pandas as pd
from fastapi import FastAPI, HTTPException
from fastapi.responses import JSONResponse
from pydantic import ValidationError
from typing import Optional
from dotenv import load_dotenv

# Reuse your existing code
from analytics.src.score_one import load_bundle, fetch_context, upsert_score, TS_FMT
from analytics.src.common import sql_engine
from analytics.src.features import engineer

# Load envs (optional .env for local dev)
load_dotenv()

# Singletons (load once)
app = FastAPI(title="Fraud Scoring API", version="1.0.0")
ENGINE = sql_engine()
BUNDLE = load_bundle()
MODEL = BUNDLE["model"]
THRESHOLD = float(BUNDLE.get("threshold", 0.5))
FEAT_COLS = BUNDLE["features"]

@app.get("/health")
def health():
    return {"status": "ok", "model_loaded": bool(MODEL), "threshold": THRESHOLD}

@app.post("/score")
def score(payload: dict):
    # simple manual validation to avoid coupling to Pydantic file import path
    try:
        tx_id = int(payload.get("tx_id", 0))
        if tx_id <= 0:
            raise ValueError
    except Exception:
        raise HTTPException(status_code=400, detail="Invalid tx_id")

    try:
        # 1) fetch context for this tx
        df = fetch_context(ENGINE, tx_id)

        # 2) engineer features for ALL rows, pick the target one later
        X, _, meta, _ = engineer(df, is_training=False)

        # 3) align columns to training features
        for col in FEAT_COLS:
            if col not in X.columns:
                X[col] = 0.0
        X = X[FEAT_COLS].fillna(0.0)

        # 4) find index of target row
        idx = meta.index[meta["tx_id"] == tx_id]
        if len(idx) == 0:
            idx = [X.index[-1]]

        # 5) score
        if hasattr(MODEL, "predict_proba"):
            y_all = MODEL.predict_proba(X)[:, 1]
        else:
            y_all = MODEL.decision_function(X)

        score_val = float(y_all[idx[-1]])
        label_pred = bool(score_val >= THRESHOLD)

        # 6) write back to ml.TxScores (idempotent upsert)
        upsert_score(ENGINE, tx_id, score_val, label_pred)

        return JSONResponse(
            {
                "tx_id": tx_id,
                "score": score_val,
                "label_pred": label_pred,
                "threshold": THRESHOLD,
                "model": BUNDLE.get("which")
            }
        )
    except HTTPException:
        raise
    except Exception as e:
        # Avoid leaking internals; log if you have a logger
        raise HTTPException(status_code=500, detail="Scoring failed")
