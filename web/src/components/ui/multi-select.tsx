import * as React from "react"
import { X } from "lucide-react"
import { Badge } from "@/components/ui/badge"
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from "@/components/ui/command"
import { cn } from "@/lib/utils"

interface MultiSelectProps {
  value?: string[]
  onChange?: (value: string[]) => void
  placeholder?: string
  className?: string
  options?: { label: string; value: string }[]
  allowCustom?: boolean
}

export function MultiSelect({
  value = [],
  onChange,
  placeholder = "选择选项...",
  className,
  options = [],
  allowCustom = true,
  ...props
}: MultiSelectProps) {
  const [open, setOpen] = React.useState(false)
  const [inputValue, setInputValue] = React.useState("")

  const handleUnselect = (item: string) => {
    onChange?.(value.filter((i) => i !== item))
  }

  const handleKeyDown = (e: React.KeyboardEvent<HTMLDivElement>) => {
    const input = e.target as HTMLInputElement
    if (e.key === "Delete" || e.key === "Backspace") {
      if (input.value === "") {
        const newSelected = [...value]
        newSelected.pop()
        onChange?.(newSelected)
      }
    }
    if (e.key === "Escape") {
      input.blur()
    }
  }

  const handleSelect = (item: string) => {
    if (!value.includes(item)) {
      onChange?.([...value, item])
    }
    setInputValue("")
  }

  const handleInputKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === "Enter" && inputValue.trim() && allowCustom) {
      e.preventDefault()
      if (!value.includes(inputValue.trim())) {
        onChange?.([...value, inputValue.trim()])
      }
      setInputValue("")
    }
  }

  const selectables = options.filter((option) => !value.includes(option.value))

  return (
    <Command onKeyDown={handleKeyDown} className={cn("overflow-visible bg-transparent", className)} {...props}>
      <div className="group border border-input px-3 py-2 text-sm ring-offset-background rounded-md focus-within:ring-2 focus-within:ring-ring focus-within:ring-offset-2">
        <div className="flex gap-1 flex-wrap">
          {value.map((item) => {
            return (
              <Badge key={item} variant="secondary">
                {item}
                <button
                  className="ml-1 ring-offset-background rounded-full outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2"
                  onKeyDown={(e) => {
                    if (e.key === "Enter") {
                      handleUnselect(item)
                    }
                  }}
                  onMouseDown={(e) => {
                    e.preventDefault()
                    e.stopPropagation()
                  }}
                  onClick={() => handleUnselect(item)}
                >
                  <X className="h-3 w-3 text-muted-foreground hover:text-foreground" />
                </button>
              </Badge>
            )
          })}
          <CommandInput
            value={inputValue}
            onValueChange={setInputValue}
            onKeyDown={handleInputKeyDown}
            onBlur={() => setOpen(false)}
            onFocus={() => setOpen(true)}
            placeholder={value.length === 0 ? placeholder : ""}
            className="ml-2 bg-transparent outline-none placeholder:text-muted-foreground flex-1"
          />
        </div>
      </div>
      <div className="relative mt-2">
        {open && (selectables.length > 0 || (allowCustom && inputValue.trim())) ? (
          <div className="absolute w-full z-10 top-0 rounded-md border bg-popover text-popover-foreground shadow-md outline-none animate-in">
            <CommandList>
              {selectables.length === 0 && !allowCustom ? (
                <CommandEmpty>没有找到选项</CommandEmpty>
              ) : null}
              <CommandGroup>
                {selectables.map((option) => {
                  return (
                    <CommandItem
                      key={option.value}
                      onMouseDown={(e) => {
                        e.preventDefault()
                        e.stopPropagation()
                      }}
                      onSelect={() => handleSelect(option.value)}
                      className="cursor-pointer"
                    >
                      {option.label}
                    </CommandItem>
                  )
                })}
                {allowCustom && inputValue.trim() && !value.includes(inputValue.trim()) && (
                  <CommandItem
                    onMouseDown={(e) => {
                      e.preventDefault()
                      e.stopPropagation()
                    }}
                    onSelect={() => handleSelect(inputValue.trim())}
                    className="cursor-pointer"
                  >
                    添加 "{inputValue.trim()}"
                  </CommandItem>
                )}
              </CommandGroup>
            </CommandList>
          </div>
        ) : null}
      </div>
    </Command>
  )
}