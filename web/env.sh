#!/bin/bash
rm -rf ./env-config.js
touch ./env-config.js
 
# Add assignment 
echo "window._env_ = {" >> ./env-config.js
 
# Read each line in .env file
# Each line represents key=value pairs
while read -r line || [[ -n "$line" ]];
do
  # Split env variables by character `=`
  if printf '%s\n' "$line" | grep -q -e '='; then
    varname=$(printf '%s\n' "$line" | sed -e 's/=.*//')
    varvalue=$(printf '%s\n' "$line" | sed -e 's/^[^=]*=//')
  fi
 
  # Read value of current variable if exists as Environment variable
  value=$(printf '%s\n' "${!varname}")
  # Otherwise use value from .env file
  [[ -z $value ]] && value=${varvalue}
  
  # 添加配置到 env-config.js 文件
  echo "  $varname: \"$value\"," >> ./env-config.js
done < .env
 
echo "}" >> ./env-config.js