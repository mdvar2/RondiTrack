"""Capture actual local Aspire UI. Requires Playwright and installed Microsoft Edge.

Run after EvidenceHost prints REAL_API_EVIDENCE_READY. No DOM or image alterations.
Dashboard selectors are specific to the pinned image documented in Stage 8.
"""
import json
import re
from pathlib import Path
from playwright.sync_api import sync_playwright

root = Path(__file__).resolve().parents[2]
private = root / "obj/stage8-evidence"
data = json.loads((private / "manifest.json").read_text())
output = root / "docs/evidence"
output.mkdir(exist_ok=True)
with sync_playwright() as playwright:
    browser = playwright.chromium.launch(
        executable_path="C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
        headless=True,
    )
    page = browser.new_page(viewport={"width": 1800, "height": 1200}, device_scale_factor=1)
    page.goto("http://127.0.0.1:18888/traces/detail/" + data["payoutTrace"], wait_until="networkidle")
    page.wait_for_timeout(1000)
    page.get_by_text("payout.process", exact=True).click()
    page.wait_for_timeout(500)
    text = page.locator("body").inner_text()
    assert "RondiTrack.Business" in text and "outcome" in text and "success" in text
    assert re.search(r"Depth\s*3", text) and "postgresql" in text
    assert not any(value in text for value in (
        "EvidenceOwner", "EvidenceOutsider", "EvidenceStokvel", "4321.87", "@tests.example"
    ))
    page.screenshot(path=str(output / "payout-trace.png"), full_page=False)
    page.goto("http://127.0.0.1:18888/structuredlogs", wait_until="networkidle")
    page.wait_for_timeout(500)
    template = "Security event {EventType} with {Outcome}. CorrelationId: {CorrelationId}"
    page.get_by_text(template, exact=True).first.click()
    page.wait_for_timeout(500)
    logs = page.locator("body").inner_text()
    data["dashboardLogsReceived"] = all(value in logs for value in ("sign_in", "success", data["loginTrace"]))
    page.goto("http://127.0.0.1:18888/metrics", wait_until="networkidle")
    page.wait_for_timeout(500)
    page.locator('fluent-tree-item[title="ronditrack.payouts.processed"]').click()
    page.wait_for_timeout(1000)
    metrics = page.locator("body").inner_text()
    data["dashboardMetricsReceived"] = "ronditrack.payouts.processed" in metrics and "success" in metrics
    assert data["dashboardLogsReceived"] and data["dashboardMetricsReceived"]
    (private / "dashboard-verification.json").write_text(json.dumps(data, indent=2))
    browser.close()
print("GENUINE_SCREENSHOT_AND_THREE_SIGNAL_VERIFICATION_COMPLETE")
