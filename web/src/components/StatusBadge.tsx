import { Check, CheckCheck, Clock, AlertTriangle, Lock, Wallet } from "lucide-react"
import { Badge } from "./ui/badge"
import { formatCents } from "../lib/money"

type StatusBadgeProps = {
  status?: string | null
  arrearsCents?: number
}

export function StatusBadge({
  status,
  arrearsCents,
}: StatusBadgeProps) {
  if (arrearsCents !== undefined) {
    if (arrearsCents <= 0) return null

    return (
      <Badge variant="destructive">
        <AlertTriangle aria-hidden="true" />
        Arrears: {formatCents(arrearsCents)}
      </Badge>
    )
  }

  switch (status) {
    case "Open":
      return (
        <Badge variant="secondary">
          <Clock aria-hidden="true" />
          Open
        </Badge>
      )

    case "Closed":
      return (
        <Badge variant="outline">
          <Lock aria-hidden="true" />
          Closed
        </Badge>
      )

    case "PaidOut":
      return (
        <Badge variant="secondary">
          <Wallet aria-hidden="true" />
          Paid out
        </Badge>
      )

    case "Pending":
      return (
        <Badge variant="outline">
          <Clock aria-hidden="true" />
          Pending
        </Badge>
      )

    case "Paid":
      return (
        <Badge variant="secondary">
          <Check aria-hidden="true" />
          Paid
        </Badge>
      )

    case "Confirmed":
      return (
        <Badge variant="default">
          <CheckCheck aria-hidden="true" />
          Confirmed
        </Badge>
      )

    default:
      return status ? <Badge variant="outline">{status}</Badge> : null
  }
}