import { redirect } from "next/navigation";
import { getSession } from "@/lib/session";

export const instant = false

export default async function Home() {
  redirect((await getSession()) ? "/dashboard/member" : "/login");
}
