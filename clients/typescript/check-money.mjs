import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import ts from "typescript";

const code = ts.transpileModule(readFileSync(new URL("src/home.ts", import.meta.url), "utf8"), {
  compilerOptions: { target: ts.ScriptTarget.ES2020, module: ts.ModuleKind.ES2020 }
}).outputText;
const { totalOutstanding } = await import("data:text/javascript;base64," + Buffer.from(code).toString("base64"));
const home = amounts => ({ stokvels: amounts.map(amountOwed => ({ amountOwed })) });
assert.equal(totalOutstanding(home([])), "0");
assert.equal(totalOutstanding(home(["0.1", "0.2"])), "0.3");
assert.equal(totalOutstanding(home(["374.9", "0", "125.1"])), "500");
assert.equal(totalOutstanding(home(["9007199254740993.123456789", "0.000000001"])), "9007199254740993.12345679");
assert.throws(() => totalOutstanding(home(["NaN"])));
console.log("Exact money checks passed (empty, cents, mixed scale, beyond safe integer, invalid input).");
