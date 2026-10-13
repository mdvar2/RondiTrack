import type { paths } from "./schema.js";

export type MemberHome = paths["/api/v1/me/home"]["get"]["responses"][200]["content"]["application/json"];

/** Exact base-10 addition, including precision beyond cents; never converts money to Number. */
export function totalOutstanding(home: MemberHome): string {
  const amounts = home.stokvels.map(stokvel => stokvel.amountOwed);
  const scale = Math.max(0, ...amounts.map(amount => (amount.split(".")[1] ?? "").length));
  let total = 0n;
  for (const amount of amounts) {
    if (!/^\d+(\.\d+)?$/.test(amount)) throw new Error("Invalid non-negative money amount.");
    const [whole, fraction = ""] = amount.split(".");
    total += BigInt(whole + fraction.padEnd(scale, "0"));
  }
  if (scale === 0) return total.toString();
  const digits = total.toString().padStart(scale + 1, "0");
  const fraction = digits.slice(-scale).replace(/0+$/, "");
  return digits.slice(0, -scale) + (fraction ? "." + fraction : "");
}
