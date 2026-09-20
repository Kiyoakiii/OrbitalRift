def aboba(s)->str:

    set_lett =  ''.join(sorted(s)) # 'a' 'b' 'A'
    del_l = ''

    for l in set_lett:
        if l.upper() not in set_lett:
            del_l += l
        elif l.lower() not in set_lett:
            del_l += l
        else:
            continue

    j = 0

    for i, l in enumerate(set_lett):
        if l in del_l:
            j = i+1
        elif (l == l.upper() and l.lower() in set_lett[j, i]) or (l == l.lower() and l.upper() in set_lett[j, i]):
            return set_lett[j, i]
        
    return set_lett[j, i]


aboba('aba')

# abA
# aAb
# bAa
