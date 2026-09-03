#!/usr/bin/env python3
"""Quick RAG-style demo: ground llama3.1:8b's answer in creator_knowledge.v1.json instead
of letting it hallucinate from training data. Matches roadmap Phase 1.5 intent (structured
knowledge + query parsing), not model fine-tuning.
"""
import json
import urllib.request

KB_PATH = "/Users/louis/Yobi/Assets/StreamingAssets/CreatorKnowledge/creator_knowledge.v1.json"

def find_creator_id(kb, name):
    name = name.strip().lower()
    for c in kb["creators"]:
        if any(n.strip().lower() == name for n in c["names"]):
            return c["id"]
    for c in kb["creators"]:
        if any(name in n.strip().lower() for n in c["names"]):
            return c["id"]
    return None

def build_context(kb, creator_id, max_events=15):
    id_to_name = {c["id"]: c["names"][0] for c in kb["creators"]}
    events = [c for c in kb["collaborations"] if creator_id in c["participants"]]
    lines = []
    for e in events[:max_events]:
        names = " / ".join(id_to_name.get(p, p) for p in e["participants"])
        lines.append(f'- [{e["game"]}] {e["event"]}: {names} (source: {e["source"]})')
    return "\n".join(lines), len(events)

def ask_ollama(prompt):
    req = urllib.request.Request(
        "http://localhost:11434/api/generate",
        data=json.dumps({"model": "llama3.1:8b", "prompt": prompt, "stream": False}).encode(),
        headers={"Content-Type": "application/json"},
    )
    with urllib.request.urlopen(req, timeout=120) as resp:
        return json.loads(resp.read())["response"]

def main():
    kb = json.load(open(KB_PATH, encoding="utf-8"))
    query_creator = "常闇トワ"
    cid = find_creator_id(kb, query_creator)
    context, total = build_context(kb, cid)

    prompt = f"""你係一個VTuber資料查詢助手。只可以根據下面提供嘅「已知合作記錄」嚟回答,唔可以自己作資料。如果資料入面冇答案,要老實講"資料庫入面搵唔到"。

已知合作記錄(共{total}條,列咗頭15條):
{context}

問題:常闇トワ玩過LoL(League of Legends)未?同邊幾個人一齊玩過?請用廣東話回答,列出人名同片段來源。"""

    print("=== PROMPT ===")
    print(prompt)
    print("\n=== 本機 AI (llama3.1:8b) 回答 ===")
    print(ask_ollama(prompt))

if __name__ == "__main__":
    main()
