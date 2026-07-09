import sqlite3
import json
import os
import hashlib
from datetime import datetime, timedelta

CACHE_DB_PATH = os.path.join(os.path.dirname(__file__), 'cache', 'stock_data.db')
CACHE_EXPIRE_HOURS = 24

def init_cache():
    os.makedirs(os.path.dirname(CACHE_DB_PATH), exist_ok=True)
    
    conn = sqlite3.connect(CACHE_DB_PATH)
    cursor = conn.cursor()
    
    cursor.execute('''
        CREATE TABLE IF NOT EXISTS cache (
            key TEXT PRIMARY KEY,
            data TEXT NOT NULL,
            created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
            updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
        )
    ''')
    
    cursor.execute('''
        CREATE INDEX IF NOT EXISTS idx_cache_key ON cache(key)
    ''')
    
    cursor.execute('''
        CREATE INDEX IF NOT EXISTS idx_cache_updated_at ON cache(updated_at)
    ''')
    
    conn.commit()
    conn.close()

def get_cache_key(prefix, params):
    params_str = json.dumps(params, sort_keys=True)
    combined = f"{prefix}:{params_str}"
    return hashlib.md5(combined.encode()).hexdigest()

def set_cache(prefix, params, data):
    cache_key = get_cache_key(prefix, params)
    data_str = json.dumps(data, ensure_ascii=False)
    
    conn = sqlite3.connect(CACHE_DB_PATH)
    cursor = conn.cursor()
    
    now = datetime.now().isoformat()
    
    cursor.execute('''
        INSERT OR REPLACE INTO cache (key, data, created_at, updated_at)
        VALUES (?, ?, ?, ?)
    ''', (cache_key, data_str, now, now))
    
    conn.commit()
    conn.close()

def get_cache(prefix, params):
    cache_key = get_cache_key(prefix, params)
    
    conn = sqlite3.connect(CACHE_DB_PATH)
    cursor = conn.cursor()
    
    cursor.execute('''
        SELECT data, updated_at FROM cache WHERE key = ?
    ''', (cache_key,))
    
    result = cursor.fetchone()
    conn.close()
    
    if result:
        data_str, updated_at = result
        updated_time = datetime.fromisoformat(updated_at)
        
        if (datetime.now() - updated_time) < timedelta(hours=CACHE_EXPIRE_HOURS):
            try:
                return json.loads(data_str)
            except json.JSONDecodeError:
                return None
        else:
            return None
    
    return None

def clear_cache():
    conn = sqlite3.connect(CACHE_DB_PATH)
    cursor = conn.cursor()
    
    cursor.execute('DELETE FROM cache')
    conn.commit()
    conn.close()

def clear_expired_cache():
    conn = sqlite3.connect(CACHE_DB_PATH)
    cursor = conn.cursor()
    
    expire_time = (datetime.now() - timedelta(hours=CACHE_EXPIRE_HOURS)).isoformat()
    cursor.execute('DELETE FROM cache WHERE updated_at < ?', (expire_time,))
    
    conn.commit()
    conn.close()

def get_cache_stats():
    conn = sqlite3.connect(CACHE_DB_PATH)
    cursor = conn.cursor()
    
    cursor.execute('SELECT COUNT(*) FROM cache')
    total = cursor.fetchone()[0]
    
    cursor.execute('SELECT COUNT(*) FROM cache WHERE updated_at > ?', 
                   ((datetime.now() - timedelta(hours=CACHE_EXPIRE_HOURS)).isoformat(),))
    valid = cursor.fetchone()[0]
    
    conn.close()
    
    return {
        'total': total,
        'valid': valid,
        'expired': total - valid
    }

init_cache()
